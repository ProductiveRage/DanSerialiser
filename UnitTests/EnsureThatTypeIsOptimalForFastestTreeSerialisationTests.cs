using System;
using System.Reflection;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class EnsureThatTypeIsOptimalForFastestTreeSerialisationTests
	{
		[Fact]
		public static void String()
		{
			FastestTreeBinarySerialisation.EnsureThatTypeIsOptimalForFastestTreeSerialisation(typeof(string), new IFastSerialisationTypeConverter[0]);
		}

		[Fact]
		public static void SealedClassWithNoProperties()
		{
			FastestTreeBinarySerialisation.EnsureThatTypeIsOptimalForFastestTreeSerialisation(typeof(ExampleOfSealedClassWithNoProperties), new IFastSerialisationTypeConverter[0]);
		}

		/// <summary>
		/// This tests a few special cases - arrays and nullables and enums
		/// </summary>
		[Fact]
		public static void SealedClassWithNoPropertyThatIsArrayOfNullableEnum()
		{
			FastestTreeBinarySerialisation.EnsureThatTypeIsOptimalForFastestTreeSerialisation(typeof(ExampleOfSealedClassWithNullableEnumArrayProperty), new IFastSerialisationTypeConverter[0]);
		}

		[Fact]
		public static void UnsealedClassWithNoProperties()
		{
			FastestTreeSerialisationNotPossibleException(
				() => FastestTreeBinarySerialisation.EnsureThatTypeIsOptimalForFastestTreeSerialisation(typeof(ExampleOfUnsealedClassWithNoProperties), new IFastSerialisationTypeConverter[0]),
				typeof(ExampleOfUnsealedClassWithNoProperties),
				null
			);
		}

		private static void FastestTreeSerialisationNotPossibleException(Action testCode, Type expectedTargetType, MemberInfo expectedMemberIfAny)
		{
			var thrown = Assert.Throws<FastestTreeSerialisationNotPossibleException>(testCode);
			Assert.Equal(expectedTargetType.AssemblyQualifiedName, thrown.TypeName);
			if (expectedMemberIfAny == null)
				return;

			Type memberType;
			if (expectedMemberIfAny is FieldInfo field)
				memberType = field.FieldType;
			else if (expectedMemberIfAny is PropertyInfo property)
				memberType = property.PropertyType;
			else
				throw new NotSupportedException("Unsupported MemberInfo: " + expectedMemberIfAny.GetType().Name);
			Assert.Equal(expectedMemberIfAny.Name, thrown.MemberIfAny?.Name);
			Assert.Equal(memberType.AssemblyQualifiedName, thrown.MemberIfAny?.TypeName);
		}

		private class ExampleOfUnsealedClassWithNoProperties { }

		private sealed class ExampleOfSealedClassWithNoProperties { }

		private sealed class ExampleOfSealedClassWithNullableEnumArrayProperty
		{
			public DayOfWeek?[] Days { get; set; }
		}
	}
}