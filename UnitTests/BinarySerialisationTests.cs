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
		public static void PrivateSealedClassWithNoMembers()
		{
			var clone = Clone(new ClassWithNoMembersAndNoInheritance());
			Assert.NotNull(clone);
			Assert.Equal(typeof(ClassWithNoMembersAndNoInheritance), clone.GetType());
		}

		[Fact]
		public static void PrivateNullSealedClassWithNoMembers()
		{
			var clone = Clone((ClassWithNoMembersAndNoInheritance)null);
			Assert.Null(clone);
		}

		[Fact]
		public static void PrivateStructClassWithNoMembers()
		{
			var clone = Clone(new StructWithNoMembers());
			Assert.Equal(typeof(StructWithNoMembers), clone.GetType());
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

		private sealed class ClassWithNoMembersAndNoInheritance { }

		private struct StructWithNoMembers { }
	}
}