using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DanSerialiserAnalyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class DeprecatedAnalyser : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "DanSer001";
		private const string Category = "Design";
		public static DiagnosticDescriptor ReplacedByMustBeNameofPropertyRule = new DiagnosticDescriptor(
			DiagnosticId,
			"Deprecated Analyser",
			"The 'replacedBy' argument on a [Deprecated] attribute must specify a field or property defined within the same class and it must do so using nameof",
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(ReplacedByMustBeNameofPropertyRule);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForFieldWithMultipleDeprecatedAttributes, SyntaxKind.Attribute);
		}

		private void LookForFieldWithMultipleDeprecatedAttributes(SyntaxNodeAnalysisContext context)
		{
			if (!(context.Node is AttributeSyntax attribute))
				return;

			var ownerOfAttributes = ((attribute.Parent as AttributeListSyntax)?.Parent as MemberDeclarationSyntax);
			SyntaxList<AttributeListSyntax> attributeLists;
			if (ownerOfAttributes is BaseFieldDeclarationSyntax field)
				attributeLists = field.AttributeLists;
			else if (ownerOfAttributes is BasePropertyDeclarationSyntax property)
				attributeLists = property.AttributeLists;
			else
				return;

			var allAttributes = attributeLists.SelectMany(a => a.Attributes);
			var deprecatedAttributesWithSingleArgument = allAttributes
				.Where(a => (a.ArgumentList != null) && (a.ArgumentList.Arguments.Count == 1))
				.Select(a =>
				{
					if (a.Name is IdentifierNameSyntax identifier)
						return new { Attribute = a, identifier.Identifier };
					else if (a.Name is QualifiedNameSyntax qualifierIdentifier)
						return new { Attribute = a, qualifierIdentifier.Right.Identifier };
					else
						return null;
				})
				.Where(a => a != null)
				.Where(a => (a.Identifier.Text == "Deprecated") || (a.Identifier.Text == "DeprecatedAttribute"))
				.Select(a => a.Attribute);
			if (!deprecatedAttributesWithSingleArgument.Any())
				return;

			foreach (var deprecatedAttribute in deprecatedAttributesWithSingleArgument)
			{
				// Ensure that this is the [Deprecated] attribute from DanSerialiser and not one from somewhere else (if can't resolve it then presume that there is
				// a problem with the code and wait until it all compiles properly)
				// Note: Don't need to check that argument name since [Deprecated] only has one ctor signature that takes a single argument (which is "replacedBy")
				var namespaceSegments = new List<string>();
				var containingNamespace = (context.SemanticModel.GetSymbolInfo(deprecatedAttribute).Symbol as IMethodSymbol)?.ContainingNamespace;
				while (!string.IsNullOrEmpty(containingNamespace?.Name))
				{
					namespaceSegments.Insert(0, containingNamespace.Name);
					containingNamespace = containingNamespace.ContainingNamespace;
				}
				if (string.Join(".", namespaceSegments) != "DanSerialiser.Attributes")
					continue;

				// The "replacedBy" value must indicate a Field or Property. Technically, it would be ok to specify the name with a string literal or with a string
				// constant but the best way is to use nameof(..) since that's more refactor-friendly and so that's the only way that I'm going to accept.
				var argumentExpression = deprecatedAttribute.ArgumentList.Arguments[0].Expression;
				if (((argumentExpression is InvocationExpressionSyntax invocation))
				&& (invocation.Expression is IdentifierNameSyntax invocationTargetName)
				&& (invocationTargetName.Identifier.Text == "nameof")
				&& (invocation.ArgumentList != null)
				&& (invocation.ArgumentList.Arguments != null)
				&& (invocation.ArgumentList.Arguments.Count == 1))
				{
					var nameOfTargetSymbol = context.SemanticModel.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression).Symbol;
					if (nameOfTargetSymbol != null)
					{
						ISymbol target;
						if ((nameOfTargetSymbol is IPropertySymbol targetProperty) && !targetProperty.Parameters.Any())
							target = targetProperty;
						else if (nameOfTargetSymbol is IFieldSymbol fieldProperty)
							target = fieldProperty;
						else
							target = null;
						if (target != null)
						{
							// For now, the BinarySerialisationReader presumes that the 'replacedBy' target will be declared on the same class as the property that is
							// [Deprecated] and so we need to check for that (this might be relaxed in the future so that the target can be elsewhere, such as on a
							// base class, but since that assumption is currently in play it's best to test for it)
							if (target.ContainingType == null)
							{
								// If can't resolve the property's ContainingType then presume that the code is invalid and leave worrying about this until the code compiles
								continue;
							}
							var ownerSymbol = context.SemanticModel.GetDeclaredSymbol(ownerOfAttributes);
							if ((ownerSymbol == null) || (ownerSymbol.ContainingType == null))
							{
								// If can't resolve the owner reference then presume that the code is invalid
								return;
							}
							if (ownerSymbol.ContainingType == target.ContainingType)
								continue;
						}
					}
				}

				context.ReportDiagnostic(Diagnostic.Create(
					ReplacedByMustBeNameofPropertyRule,
					context.Node.GetLocation()
				));
			}
		}
	}
}