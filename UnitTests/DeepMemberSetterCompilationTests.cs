using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using DanSerialiser;
using Xunit;
using static DanSerialiser.CachedLookups.BinarySerialisationDeepCompiledMemberSetters;

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
				new SealedPersonDetailsWithSealedNameDetails { Name = new SealedNameDetails { Name = "Test" } },
				expectedNumberOfMemberSettersGenerated: 2
			);

			/// <summary>
			/// There is no ambiguity about types when a property type is a struct (can't derive from a struct so a property that is a struct type will always have
			/// a value that is precisely that type)
			/// </summary>
			[Fact]
			public static void ClassWithSinglePropertyThatIsStructWithSinglePrimitiveProperty() => AssertCanGenerateCorrectMemberSetter(
				new SealedPersonDetailsWithStructNameDetails { Name = new StructNameDetails { Name = "Test" } },
				expectedNumberOfMemberSettersGenerated: 2
			);

			[Fact]
			public static void NestedTypesWillWorkInOneDimensionalArrayTypes() => AssertCanGenerateCorrectMemberSetter(
				new ContactListDetails { Names = new[] { new SealedNameDetails { Name = "Test" }, new SealedNameDetails { Name = "Test" } } },
				expectedNumberOfMemberSettersGenerated: 2
			);

			[Fact]
			public static void ClassWithSimplePropertyThatIsSealedTypeThatUsesAllPrimitiveLikeTypes() => AssertCanGenerateCorrectMemberSetter(
				new SomethingWithPropertyOfTypeWithEveryPrimitiveEsqueType {
					Value = new SomethingWithAllSimpleTypesFields
					{
						Value1 = true, Value2 = 1, Value3 = 12, Value4 = 123, Value5 = 1234, Value6 = 12345, Value7 = 123, Value8 = 1234, Value9 = 12345, Value10 = 1.23f,
						Value11 = 12.34, Value12 = 123.45m, Value13 = 'a', Value14 = "abc", Value15 = new DateTime(2018, 10, 9, 12, 28, 53),
						Value16 = new TimeSpan(0, 12, 29, 1, 123), Value17 = new Guid("E1E06164-0477-4FF7-AD79-86772AE5EF7A"),

						Values1 = new[] { true }, Values2 = new[] { (byte)1 }, Values3 = new[] { (sbyte)12 }, Values4 = new[] { (short)123 }, Values5 = new[] { 1234 },
						Values6 = new[] { (long)12345 }, Values7 = new[] { (ushort)123 }, Values8 = new[] { (uint)1234 }, Values9 = new[] { (ulong)12345 },
						Values10 = new[] { 1.23f }, Values11 = new[] { 12.34 }, Values12 = new[] { 123.45m }, Values13 = new[] { 'a' }, Values14 = new[] { "abc" },
						Values15 = new[] { new DateTime(2018, 10, 9, 12, 28, 53) }, Values16 = new[] { new TimeSpan(0, 12, 29, 1, 123) },
						Values17 = new[] { new Guid("E1E06164-0477-4FF7-AD79-86772AE5EF7A") }
					}
				},
				expectedNumberOfMemberSettersGenerated: 2
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

		private static void AssertCanGenerateCorrectMemberSetter(object source, int expectedNumberOfMemberSettersGenerated)
		{
			// TryToGenerateMemberSetters will try to return member setters for each type that it encountered while analysing the source type - for example, if
			// source is an instance of "PersonDetails" and if "PersonDetails" has an int Key property and a "NameDetails" Name property where "NameDetails" is
			// a class with a single string property "Name" then TryToGenerateMemberSetters will try to generate member setters for both the "PersonDetails"
			// and "NameDetails" classes (it may not succeed, in which case it may return zero or one member setters, or it may succeed completely and return
			// two member setters). It won't return member setters for values that have first class IWrite support (primitives, strings, DateTime, etc..)
			var sourceType = source.GetType();
			var deepMemberSetterCache = new ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults>();
			var memberSetterDetailsForAllTypesInvolved = GetMemberSettersFor(
				sourceType,
				new IFastSerialisationTypeConverter[0],
				deepMemberSetterCache
			);
			Assert.NotNull(memberSetterDetailsForAllTypesInvolved); // We should always get a non-null reference for this (but doesn't hurt to confirm)

			// Try to get member setter for the source type
			if (!memberSetterDetailsForAllTypesInvolved.MemberSetters.TryGetValue(sourceType, out var memberSetterDetailsForType))
				memberSetterDetailsForType = null;
			Assert.NotNull(memberSetterDetailsForType);

			// We should know how many member setters we expected to be generated, so let's confirm that
			Assert.Equal(expectedNumberOfMemberSettersGenerated, memberSetterDetailsForAllTypesInvolved.MemberSetters.Count(kvp => kvp.Value != null));

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
			var deepMemberSetterCache = new ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults>();
			var memberSetterDetailsForAllTypesInvolved = GetMemberSettersFor(
				sourceType,
				new IFastSerialisationTypeConverter[0],
				deepMemberSetterCache
			);
			Assert.NotNull(memberSetterDetailsForAllTypesInvolved); // This should never be null, even if it doesn't contain a member setter for the target type

			// If the BinarySerialisationDeepCompiledMemberSetters failed to produce a member setter then it will include a null value in the MemberSetters dictionary
			// to indicate that it is not possible (this is intentional, for cases where the BinarySerialisationWriter tries to generate "deep" member setters, it's
			// useful to let it know which types it WAS possible to generate them for and which it was NOT so that no effort is wasted later on in trying again to
			// generate missing member setters).
			Assert.True(memberSetterDetailsForAllTypesInvolved.MemberSetters.TryGetValue(sourceType, out var memberSetterForType));
			Assert.Null(memberSetterForType);
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

		private sealed class SomethingWithPropertyOfTypeWithEveryPrimitiveEsqueType
		{
			public SomethingWithAllSimpleTypesFields Value { get; set; }
		}

		private sealed class SomethingWithAllSimpleTypesFields
		{
			public bool Value1 { get; set; }
			public byte Value2 { get; set; }
			public sbyte Value3 { get; set; }
			public short Value4 { get; set; }
			public int Value5 { get; set; }
			public long Value6 { get; set; }
			public ushort Value7 { get; set; }
			public uint Value8 { get; set; }
			public ulong Value9 { get; set; }
			public float Value10 { get; set; }
			public double Value11 { get; set; }
			public decimal Value12 { get; set; }
			public char Value13 { get; set; }
			public string Value14 { get; set; }
			public DateTime Value15 { get; set; }
			public TimeSpan Value16 { get; set; }
			public Guid Value17 { get; set; }

			public bool[] Values1 { get; set; }
			public byte[] Values2 { get; set; }
			public sbyte[] Values3 { get; set; }
			public short[] Values4 { get; set; }
			public int[] Values5 { get; set; }
			public long[] Values6 { get; set; }
			public ushort[] Values7 { get; set; }
			public uint[] Values8 { get; set; }
			public ulong[] Values9 { get; set; }
			public float[] Values10 { get; set; }
			public double[] Values11 { get; set; }
			public decimal[] Values12 { get; set; }
			public char[] Values13 { get; set; }
			public string[] Values14 { get; set; }
			public DateTime[] Values15 { get; set; }
			public TimeSpan[] Values16 { get; set; }
			public Guid[] Values17 { get; set; }
		}
	}
}