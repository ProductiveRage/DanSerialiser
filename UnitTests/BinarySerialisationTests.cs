using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class BinarySerialisationTests
	{
		[Fact]
		public static void Int32()
		{
			AssertCloneMatchesOriginal(32);
		}

		[Fact]
		public static void NullString()
		{
			AssertCloneMatchesOriginal((string)null);
		}

		[Fact]
		public static void BlankString()
		{
			AssertCloneMatchesOriginal("");
		}

		[Fact]
		public static void String()
		{
			AssertCloneMatchesOriginal("Café");
		}

		[Fact]
		public static void SealedClassWithNoProperties()
		{
			var clone = Clone(new ClassWithNoPropertiesAndNoInheritance());
			Assert.NotNull(clone);
			Assert.Equal(typeof(ClassWithNoPropertiesAndNoInheritance), clone.GetType());
		}

		[Fact]
		public static void NullSealedClassWithNoProperties()
		{
			var clone = Clone((ClassWithNoPropertiesAndNoInheritance)null);
			Assert.Null(clone);
		}

		private static void AssertCloneMatchesOriginal<T>(T value)
		{
			var clone = Clone(value);
			Assert.Equal(value, clone);
		}

		private static T Clone<T>(T value)
		{
			var writer = new BinaryWriter();
			Serialiser.Instance.Serialise(value, writer);
			var reader = new BinaryReader(writer.GetData());
			return reader.Read<T>();
		}

		private sealed class ClassWithNoPropertiesAndNoInheritance { }
	}
}