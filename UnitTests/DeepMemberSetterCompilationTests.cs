using System;
using System.IO;
using System.Linq;
using DanSerialiser;
using DanSerialiser.CachedLookups;
using Xunit;

namespace UnitTests
{
	public static class DeepMemberSetterCompilationTests
	{
		public static class CanGenerate
		{
			/// <summary>
			/// The analysis of the types used is performed on the target type only, not the instance of the type which is to be serialised, which means that if any
			/// nested types can not be unambiguously known at analysis time then it will not be possible to construct a member setter for it - if a nested type is
			/// sealed then it's not a problem because a property of SealedTypeA can only ever be an instance of SealedTypeA (or null) but if a property is TypeB
			/// and TypeB is not sealed then the property value to be serialised MIGHT be an instance of TypeB or it might be an instance of TypeDerivedFromTypeB,
			/// we can't know. This test is for the happier case, where the nested type is sealed.
			/// </summary>
			[Fact]
			public static void ClassWithSinglePropertyThatIsSealedClassWithSinglePrimitiveProperty() => AssertCanGenerateCorrectMemberSetter(
				new SealedPersonDetailsWithSealedNameDetails { Name = new SealedNameDetails { Name = "Test" } }
			);

			/// <summary>
			/// There is no ambiguity about types when a property type is a struct (can't derive from a struct so a property that is a struct type will always have
			/// a value that is precisely that type)
			/// </summary>
			[Fact]
			public static void ClassWithSinglePropertyThatIsStructWithSinglePrimitiveProperty() => AssertCanGenerateCorrectMemberSetter(
				new SealedPersonDetailsWithStructNameDetails { Name = new StructNameDetails { Name = "Test" } }
			);

			/// <summary>
			/// If a class has a property that is an unsealed class but that property is has a SpecialisationsMayBeIgnoredWhenSerialising attribute on it then
			/// it means that the potential for specialisations of that class may be ignored (it may be treated as if that property class was sealed, there will
			/// be no ambiguity at analysis time about what type it should be serialised as)
			/// </summary>
			[Fact]
			public static void ClassWithSinglePropertyThatIsNonSealedTypeWhereSpecialisationsMayBeIgnored() => AssertCanGenerateCorrectMemberSetter(
				new SealedPersonDetailsWithUnsealedButSpecialisationIgnoredNameDetails { Name = new UnsealedNameDetails { Name = "Test" } }
			);

			[Fact]
			public static void NestedTypesWillWorkInOneDimensionalArrayTypes() => AssertCanGenerateCorrectMemberSetter(
				new ContactListDetails { Names = new[] { new SealedNameDetails { Name = "Test" }, new SealedNameDetails { Name = "Test" } } }
			);
		}

		public static class CanNotGenerate
		{
			/// <summary>
			/// This is a counterpart to ClassWithSinglePropertyThatIsSealedTypeWithSinglePrimitiveProperty that illustrates that it won't be possible to generate a
			/// member setter for a type that has a property whose type is a non-sealed class
			/// </summary>
			[Fact]
			public static void ClassWithSinglePropertyThatIsNonSealedClassWithSinglePrimitiveProperty() => AssertCanGenerateNotCorrectMemberSetter(typeof(SealedPersonDetailsWithUnsealedNameDetails));

			[Fact]
			public static void ClassWithSinglePropertyThatIsAbstractClassWithSinglePrimitiveProperty() => AssertCanGenerateNotCorrectMemberSetter(typeof(SealedPersonDetailsWithAbstractNameDetails));
		}

		private static void AssertCanGenerateCorrectMemberSetter(object source)
		{
			// TryToGenerateMemberSetters will try to return member setters for each type that it encountered while analysing the source type - for example, if
			// source is an instance of "PersonDetails" and if "PersonDetails" has an int Key property and a "NameDetails" Name property where "NameDetails" is
			// a class with a single string property "Name" then TryToGenerateMemberSetters will try to generate member setters for both the "PersonDetails"
			// and "NameDetails" classes (it may not succeed, in which case it may return zero or one member setters, or it may succeed completely and return
			// two member setters). It won't return member setters for values that have first class IWrite support (primitives, strings, DateTime, etc..)
			var sourceType = source.GetType();
			var memberSetterDetailsForAllTypesInvolved = TryToGenerateMemberSetters(sourceType);
			Assert.NotNull(memberSetterDetailsForAllTypesInvolved); // We should always get a non-null reference for this (but doesn't hurt to confirm)

			// Try to get member setter for the source type
			if (!memberSetterDetailsForAllTypesInvolved.MemberSetters.TryGetValue(sourceType, out var memberSetterDetailsForType))
				memberSetterDetailsForType = null;
			Assert.NotNull(memberSetterDetailsForType);

			byte[] serialised;
			using (var stream = new MemoryStream())
			{
				// See notes in SimpleMemberSetterCompilationTests's "AssertCanGenerateCorrectMemberSetter" method - this code is relying more on implementation
				// details that I would like but it seems like the least of all evils to do so
				foreach (var typeName in memberSetterDetailsForAllTypesInvolved.TypeNamesToDeclare)
				{
					var typeNameBytes = new[] { (byte)BinarySerialisationDataType.FieldNamePreLoad }.Concat(typeName.AsStringAndReferenceID).ToArray();
					stream.Write(typeNameBytes, 0, typeNameBytes.Length);
				}
				foreach (var fieldName in memberSetterDetailsForAllTypesInvolved.FieldNamesToDeclare)
				{
					var fieldNameBytes = new[] { (byte)BinarySerialisationDataType.FieldNamePreLoad }.Concat(fieldName.AsStringAndReferenceID).ToArray();
					stream.Write(fieldNameBytes, 0, fieldNameBytes.Length);
				}
				var writer = new BinarySerialisationWriter(stream);
				writer.ObjectStart(source.GetType());
				memberSetterDetailsForType(source, writer);
				writer.ObjectEnd();
				serialised = stream.ToArray();
			}
			var clone = BinarySerialisation.Deserialise<object>(serialised);
			if (!ObjectComparer.AreEqual(source, clone, out var differenceSummaryIfNotEqual))
				throw new Exception("Clone failed: " + differenceSummaryIfNotEqual);
		}

		private static void AssertCanGenerateNotCorrectMemberSetter(Type sourceType)
		{
			var memberSetterDetailsForAllTypesInvolved = TryToGenerateMemberSetters(sourceType);
			Assert.NotNull(memberSetterDetailsForAllTypesInvolved); // This should never be null, even if it doesn't contain a member setter for the target type

			// If the BinarySerialisationDeepCompiledMemberSetters failed to produce a member setter then it will include a null value in the MemberSetters dictionary
			// to indicate that it is not possible (this is intentional, for cases where the BinarySerialisationWriter tries to generate "deep" member setters, it's
			// useful to let it know which types it WAS possible to generate them for and which it was NOT so that no effort is wasted later on in trying again to
			// generate missing member setters).
			Assert.True(memberSetterDetailsForAllTypesInvolved.MemberSetters.TryGetValue(sourceType, out var memberSetterForType));
			Assert.Null(memberSetterForType);
		}

		private static readonly object _lock = new object();
		private static BinarySerialisationDeepCompiledMemberSetters.DeepCompiledMemberSettersGenerationResults TryToGenerateMemberSetters(Type type)
		{
			// Don't expect the tests to be run in parallel but things could go awry if they did (because BinarySerialisationDeepCompiledMemberSetters is a static
			// class with a shared lookup of member setters that it builds, that we clear out before each run)
			lock (_lock)
			{
				BinarySerialisationDeepCompiledMemberSetters.ClearCache();
				return BinarySerialisationDeepCompiledMemberSetters.GetMemberSettersFor(type);
			}
		}

		private sealed class SealedPersonDetailsWithSealedNameDetails
		{
			public SealedNameDetails Name { get; set; }
		}

		private sealed class SealedPersonDetailsWithUnsealedNameDetails
		{
			public UnsealedNameDetails Name { get; set; }
		}

		private sealed class SealedPersonDetailsWithAbstractNameDetails
		{
			public AbstractNameDetails Name { get; set; }
		}

		private sealed class SealedPersonDetailsWithStructNameDetails
		{
			public StructNameDetails Name { get; set; }
		}

		private sealed class SealedPersonDetailsWithUnsealedButSpecialisationIgnoredNameDetails
		{
			[SpecialisationsMayBeIgnoredWhenSerialising]
			public UnsealedNameDetails Name { get; set; }
		}

		private sealed class ContactListDetails
		{
			public SealedNameDetails[] Names { get; set; }
		}

		private sealed class SealedNameDetails
		{
			public string Name { get; set; }
		}

		private class UnsealedNameDetails
		{
			public string Name { get; set; }
		}

		public abstract class AbstractNameDetails
		{
			public string Name { get; set; }
		}

		public struct StructNameDetails
		{
			public string Name { get; set; }
		}
	}
}