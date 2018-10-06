using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;

namespace DanSerialiserAnalyser.UnitTests
{
	public class DeprecatedAnalyserTests : DiagnosticVerifier
	{
		[Fact]
		public void IdealUsageForTargetProperty()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated(replacedBy: nameof(Something)]
						public int ID { get { return Something; } set { Something = value; } }

						public int Something { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[Fact]
		public void IdealUsageForTargetField()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated(replacedBy: nameof(Something)]
						public int ID { get { return Something; } set { Something = value; } }

						public int Something;
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[Fact]
		public void NoArguments()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated]
						public int ID { get { return Something; } }

						public int Something { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[Fact]
		public void NoArgumentsWithBrackets()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated()]
						public int ID { get { return Something; } }

						public int Something { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// It could be argued that this approach is valid but using nameof(..) is better because it's more refactor friendly and so the analyser requires nameof
		/// </summary>
		[Fact]
		public void SpecifyingThePropertyByStringIsNotOk()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated(replacedBy: ""Something"")]
						public int ID { get { return Something; } set { Something = value; } }

						public int Something { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = DeprecatedAnalyser.DiagnosticId,
				Message = string.Format(DeprecatedAnalyser.ReplacedByMustBeNameofPropertyRule.MessageFormat.ToString()),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[Fact]
		public void ReplacedByNameofTargetMustBeProperty()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass
					{
						[Deprecated(replacedBy: nameof(MyClass)]
						public int ID { get { return Something; } set { Something = value; } }

						public int Something { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = DeprecatedAnalyser.DiagnosticId,
				Message = string.Format(DeprecatedAnalyser.ReplacedByMustBeNameofPropertyRule.MessageFormat.ToString()),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		/// <summary>
		/// It keeps everything simpler if the target property must be declared on the same class (this restriction might be relaxed in the future but the serialisation process assumes 
		/// it for now and so it's best to check for it)
		/// </summary>
		[Fact]
		public void ReplacedByNameofTargetMustBePropertyOnSameClass()
		{
			var testContent = @"
				using DanSerialiser.Attributes;

				namespace TestCase
				{
					public class MyClass : MyBaseClass
					{
						[Deprecated(replacedBy: nameof(Something)]
						public int ID { get { return Something; } set { Something = value; } }
					}

					public class MyBaseClass
					{
						public int Something { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = DeprecatedAnalyser.DiagnosticId,
				Message = string.Format(DeprecatedAnalyser.ReplacedByMustBeNameofPropertyRule.MessageFormat.ToString()),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new DeprecatedAnalyser();
		}
	}
}