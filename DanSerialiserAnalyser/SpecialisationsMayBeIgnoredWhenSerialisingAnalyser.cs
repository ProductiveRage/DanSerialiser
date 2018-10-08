using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DanSerialiserAnalyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class SpecialisationsMayBeIgnoredWhenSerialisingAnalyser : DiagnosticAnalyzer
	{
		public const string DiagnosticId_NotAbstractClassesOrInterfaces = "DanSer002";
		public const string DiagnosticId_NotSealedTypes = "DanSer003";
		private const string Category = "Design";
		public static DiagnosticDescriptor NotApplicableToAbstractCLassesOrInterfaces = new DiagnosticDescriptor(
			DiagnosticId_NotAbstractClassesOrInterfaces,
			"SpecialisationsMayBeIgnoredWhenSerialising Analyser",
			"The [SpecialisationsMayBeIgnoredWhenSerialising] attribute may not be applied to fields or properties whose types are interfaces or abstract classes",
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor NotApplicableToSealedTypes = new DiagnosticDescriptor(
			 DiagnosticId_NotSealedTypes,
			 "SpecialisationsMayBeIgnoredWhenSerialising Analyser",
			 "The [SpecialisationsMayBeIgnoredWhenSerialising] attribute may not be applied to fields or properties whose types may not be specialised (such as primitives, strings, value types and sealed classes)",
			 Category,
			 DiagnosticSeverity.Error,
			 isEnabledByDefault: true
		 );

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(NotApplicableToAbstractCLassesOrInterfaces, NotApplicableToSealedTypes);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForInvalidSpecialisationsMayBeIgnoredWhenSerialisingAttributes, SyntaxKind.Attribute);
		}

		private void LookForInvalidSpecialisationsMayBeIgnoredWhenSerialisingAttributes(SyntaxNodeAnalysisContext context)
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
			var specialisationsMayBeIgnoredWhenSerialisingAttributes = allAttributes
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
				.Where(a => (a.Identifier.Text == "SpecialisationsMayBeIgnoredWhenSerialising") || (a.Identifier.Text == "SpecialisationsMayBeIgnoredWhenSerialisingAttribute"))
				.Select(a => a.Attribute);
			if (!specialisationsMayBeIgnoredWhenSerialisingAttributes.Any())
				return;

			foreach (var specialisationsMayBeIgnoredWhenSerialisingAttribute in specialisationsMayBeIgnoredWhenSerialisingAttributes)
			{
				// Walk up the tree until we get to the field or property that this attribute is on (if this fails then skip the check for now on the basis that it's probably
				// a problem with the code somehow and that once it's fixed and compiles properly that we'll be able to verify)
				SyntaxNode node = specialisationsMayBeIgnoredWhenSerialisingAttribute;
				while ((node != null) && !(node is FieldDeclarationSyntax) && !(node is PropertyDeclarationSyntax))
					node = node.Parent;
				var type = (node as PropertyDeclarationSyntax)?.Type ?? (node as FieldDeclarationSyntax)?.Declaration?.Type;
				if (type == null)
					continue;

				// Ensure that this is the [SpecialisationsMayBeIgnoredWhenSerialising] attribute from DanSerialiser and not one from somewhere else (if can't resolve it then
				// presume that there is a problem with the code and wait until it all compiles properly)
				if ((!(context.SemanticModel.GetSymbolInfo(specialisationsMayBeIgnoredWhenSerialisingAttribute).Symbol is IMethodSymbol method)) || !NamespaceConfirming.IsIn(method, "DanSerialiser"))
					continue;

				var typeInfo = context.SemanticModel.GetTypeInfo(type).Type;
				if ((typeInfo == null) || (typeInfo is IErrorTypeSymbol))
					continue;

				// If typeInfo refers to an interface then IsAbstract will be true (so this is all we need to check for is-abstract-class-OR-is-interface)
				// Note: It wouldn't make sense to use this attribute on a field or property that was of a static type because static types can't be inherited from (and so can
				// not be "specialised") but it's not valid for a field or property type to be a static type and so we don't need to worry about that!
				if (typeInfo.IsAbstract)
				{
					context.ReportDiagnostic(Diagnostic.Create(
						NotApplicableToAbstractCLassesOrInterfaces,
						context.Node.GetLocation()
					));
					continue;
				}

				// The IsValueType property will be true for primitives (eg. int) and structs
				if (typeInfo.IsValueType || typeInfo.IsSealed)
				{
					context.ReportDiagnostic(Diagnostic.Create(
						NotApplicableToSealedTypes,
						context.Node.GetLocation()
					));
					continue;
				}
			}
		}
	}
}