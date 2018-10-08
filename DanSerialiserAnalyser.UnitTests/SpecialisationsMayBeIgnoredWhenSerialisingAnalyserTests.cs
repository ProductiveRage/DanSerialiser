using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;

namespace DanSerialiserAnalyser.UnitTests
{
	public class SpecialisationsMayBeIgnoredWhenSerialisingAnalyserTests : DiagnosticVerifier
	{
		/// <summary>
		/// A SpeedyButLimited-configured binary serialiser would be very happy with this type because it would be able to compile a LINQ expression to
		/// serialise it - without the SpecialisationsMayBeIgnoredWhenSerialising attribute it would not be able to do this because the Translations
		/// property could be set to an instance of MyDictionary (a class that is derived from the Dictionary-of-int-and-string) OR it may be set to an
		/// instance of the Dictionary but with a custom equality comparer. When unpredictable things like this may occur, it limits what 'member setters'
		/// may be compiled ahead of serialisation but when those uncertainties may be removed, optimisation is simpler. (This is ONE of the reasons that
		/// ProtoBuf is so fast - it does not attempt to perfectly replicate types and all of their properties, it fits data into the ProtoBuf protocol
		/// and so will always write a Dictionary-of-int-string as a list of key-value pairs rather than trying to maintain the Dictionary type across
		/// roundtrips or configuration options, such as an equality comparer).
		/// </summary>
		[Fact]
		public void ExampleOfIntendedUsage()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						public int DefaultValue { get; set }

						[SpecialisationsMayBeIgnoredWhenSerialising]
						public Dictionary<int, string> Translations { get; set; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[Fact]
		public void ExampleOfIntendedUsageForFields()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						[SpecialisationsMayBeIgnoredWhenSerialising]
						public Dictionary<int, string> Translations;
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[Fact]
		public void MayNotBeUsedOnInterfaceProperties()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						public int DefaultValue { get; set }

						[SpecialisationsMayBeIgnoredWhenSerialising]
						public IDictionary<int, string> Translations { get; set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.DiagnosticId_NotAbstractClassesOrInterfaces,
				Message = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.NotApplicableToAbstractCLassesOrInterfaces.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[Fact]
		public void MayNotBeUsedOnAbstractClassProperties()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						public int DefaultValue { get; set }

						[SpecialisationsMayBeIgnoredWhenSerialising]
						public TranslationDictionaryBase Translations { get; set; }
					}

					public abstract class TranslationDictionaryBase { }
				}";

			var expected = new DiagnosticResult
			{
				Id = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.DiagnosticId_NotAbstractClassesOrInterfaces,
				Message = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.NotApplicableToAbstractCLassesOrInterfaces.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[Fact]
		public void MayNotBeUsedOnPrimitiveProperties()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						[SpecialisationsMayBeIgnoredWhenSerialising]
						public int DefaultValue { get; set }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.DiagnosticId_NotSealedTypes,
				Message = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.NotApplicableToSealedTypes.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 9, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[Fact]
		public void MayNotBeUsedOnStructProperties()
		{
			var testContent = @"
				using DanSerialiser;
				using System.Collections.Generic;

				namespace TestCase
				{
					public sealed class MyClass
					{
						[SpecialisationsMayBeIgnoredWhenSerialising]
						public int? DefaultValue { get; set }
					}

					public abstract class TranslationDictionaryBase { }
				}";

			var expected = new DiagnosticResult
			{
				Id = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.DiagnosticId_NotSealedTypes,
				Message = SpecialisationsMayBeIgnoredWhenSerialisingAnalyser.NotApplicableToSealedTypes.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 9, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new SpecialisationsMayBeIgnoredWhenSerialisingAnalyser();
		}
	}
}