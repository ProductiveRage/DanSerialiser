using System;
using DanSerialiser.CachedLookups;
using DanSerialiser.Reflection;
using Xunit;
using static DanSerialiser.CachedLookups.BinarySerialisationCompiledMemberSetters;

namespace UnitTests
{
	public static class SimpleMemberSetterCompilationTests
	{
		[Fact]
		public static void ClassWithNoFieldsOrPropertiesIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingEmpty)));

		/// <summary>
		/// Test a property that is a primitive type
		/// </summary>
		[Fact]
		public static void ClassWithOnlyKeyPropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithKey)));

		[Fact]
		public static void ClassWithOnlyKeyFieldIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithKeyField)));

		/// <summary>
		/// Test a property that is a string (not a primitive but treated like one in that it was an IWrite method and references of the class are never reused)
		/// </summary>
		[Fact]
		public static void ClassWithOnlyStringPropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithID)));

		/// <summary>
		/// Test a property that is a string array - if a type has an IWrite method for it then a 1D array of that type is also supported (multi-dimensional arrays
		/// are not supported at the moment, see ClassWithOnlyStringArrayPropertyIsEasy)
		/// </summary>
		[Fact]
		public static void ClassWithOnlyStringArrayPropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithIDs)));

		/// <summary>
		/// Test a property that is a DateTime (also not a primitive but also given first class treatment by IWrite)
		/// </summary>
		[Fact]
		public static void ClassWithOnlyDateTimePropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithModifiedDate)));

		/// <summary>
		/// This is a bit of a fat test! It is more of a final sanity test, rather than a small focussed unit test..
		/// </summary>
		[Fact]
		public static void ClassWithPropertiesCoveringEverySimpleTypeAndAsOneDimensionArraysShouldWork() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithAllSimpleTypes)));

		[Fact]
		public static void PropertyOfClassWithoutFirstClassSupportInIWriteWillNotWork() => Assert.Null(TryToGenerateMemberSetter(typeof(WrapperForSomethingEmpty)));

		/// <summary>
		/// Maybe support for multi-dimensional arrays will be added in the future but it is not supported currently (the test ClassWithOnlyStringArrayPropertyIsEasy
		/// illustrates that 1D arrays of simple types are supported)
		/// </summary>
		[Fact]
		public static void PropertiesThatAreMultiDimensionalArraysWillNotWork() => Assert.Null(TryToGenerateMemberSetter(typeof(SomethingWithMap)));

		/// <summary>
		/// Similar to PropertiesThatAreMultiDimensionalArraysWillNotWork - currently jagged arrays of basic types are not supported
		/// </summary>
		[Fact]
		public static void PropertiesThatAreJaggedArraysWillNotWork() => Assert.Null(TryToGenerateMemberSetter(typeof(SomethingWithJaggedMap)));

		private static MemberSetterDetails TryToGenerateMemberSetter(Type type)
		{
			return BinarySerialisationCompiledMemberSetters.TryToGenerateMemberSetter(
				type,
				DefaultTypeAnalyser.Instance,
				valueWriterRetriever: t =>
				{
					// These tests are only for "simple" member setters - ones where properties are of types that may be serialised using IWrite methods (such as
					// Boolean, String and DateTime) and not for when nested member setters are required for fields or properties of more complex types, which is
					// when non-null values would need to be returned from a valueWriterRetriever delegate
					return null;
				}
			);
		}

		private class SomethingEmpty { }

		private class SomethingWithKey
		{
			public int Key { get; set; }
		}

		private class SomethingWithKeyField
		{
#pragma warning disable CS0649 // Don't whine about this field not being used
			public int Key;
#pragma warning restore CS0649
		}

		private class SomethingWithID
		{
			public string ID { get; set; }
		}

		private class SomethingWithIDs
		{
			public string[] IDs { get; set; }
		}

		private class SomethingWithMap
		{
			public bool[,] Items { get; set; }
		}

		public class SomethingWithJaggedMap
		{
			public bool[][] Items { get; set; }
		}

		private class SomethingWithModifiedDate
		{
			public DateTime ModifiedAt { get; set; }
		}

		private class WrapperForSomethingEmpty
		{
			public SomethingEmpty Value { get; set; }
		}

		private class SomethingWithAllSimpleTypes
		{
			bool Value1 { get; set; }
			byte Value2 { get; set; }
			sbyte Value3 { get; set; }
			short Value4 { get; set; }
			int Value5 { get; set; }
			long Value6 { get; set; }
			ushort Value7 { get; set; }
			uint Value8 { get; set; }
			ulong Value9 { get; set; }
			float Value10 { get; set; }
			double Value11 { get; set; }
			decimal Value12 { get; set; }
			char Value13 { get; set; }
			string Value14 { get; set; }
			DateTime Value15 { get; set; }
			TimeSpan Value16 { get; set; }
			Guid Value17 { get; set; }

			bool[] Values1 { get; set; }
			byte[] Values2 { get; set; }
			sbyte[] Values3 { get; set; }
			short[] Values4 { get; set; }
			int[] Values5 { get; set; }
			long[] Values6 { get; set; }
			ushort[] Values7 { get; set; }
			uint[] Values8 { get; set; }
			ulong[] Values9 { get; set; }
			float[] Values10 { get; set; }
			double[] Values11 { get; set; }
			decimal[] Values12 { get; set; }
			char[] Values13 { get; set; }
			string[] Values14 { get; set; }
			DateTime[] Values15 { get; set; }
			TimeSpan[] Values16 { get; set; }
			Guid[] Values17 { get; set; }
		}
	}
}