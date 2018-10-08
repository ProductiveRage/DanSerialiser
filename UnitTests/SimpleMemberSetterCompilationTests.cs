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
		/// Test a property that is a string (not a primitive but treated like one in that it was an IWriter method and references of the class are never reused)
		/// </summary>
		[Fact]
		public static void ClassWithOnlyStringPropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithId)));

		/// <summary>
		/// Test a property that is a DateTime (also not a primitive but also given first class treatment by IWriter)
		/// </summary>
		[Fact]
		public static void ClassWithOnlyDateTimePropertyIsEasy() => Assert.NotNull(TryToGenerateMemberSetter(typeof(SomethingWithModifiedDate)));

		[Fact]
		public static void PropertyOfClassWithoutFirstClassSupportInIWriterWillNotWork() => Assert.Null(TryToGenerateMemberSetter(typeof(WrapperForSomethingEmpty)));

		private static MemberSetterDetails TryToGenerateMemberSetter(Type type)
		{
			return BinarySerialisationCompiledMemberSetters.TryToGenerateMemberSetter(
				type,
				DefaultTypeAnalyser.Instance,
				valueWriterRetriever: t =>
				{
					// These tests are only for "simple" member setters - ones where properties are of types that may be serialised using IWriter methods (such as
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

		private class SomethingWithId
		{
			public string Id { get; set; }
		}

		private class SomethingWithModifiedDate
		{
			public DateTime ModifiedAt { get; set; }
		}

		private class WrapperForSomethingEmpty
		{
			public SomethingEmpty Value { get; set; }
		}
	}
}