using System.Collections.Generic;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class BinarySerialisationTests
	{
		[Fact]
		public static void Bool()
		{
			AssertCloneMatchesOriginal(true);
		}

		[Fact]
		public static void Byte()
		{
			AssertCloneMatchesOriginal(byte.MaxValue);
		}

		[Fact]
		public static void SByte()
		{
			AssertCloneMatchesOriginal(sbyte.MaxValue);
		}

		[Fact]
		public static void Int16()
		{
			AssertCloneMatchesOriginal(short.MaxValue);
		}

		[Fact]
		public static void Int32()
		{
			AssertCloneMatchesOriginal(int.MaxValue);
		}

		[Fact]
		public static void Int64()
		{
			AssertCloneMatchesOriginal(long.MaxValue);
		}
		[Fact]
		public static void UInt16()
		{
			AssertCloneMatchesOriginal(ushort.MaxValue);
		}

		[Fact]
		public static void UInt32()
		{
			AssertCloneMatchesOriginal(uint.MaxValue);
		}

		[Fact]
		public static void UInt64()
		{
			AssertCloneMatchesOriginal(ulong.MaxValue);
		}

		[Fact]
		public static void Enum()
		{
			AssertCloneMatchesOriginal(DefaultTypeEnum.Value2);
		}

		[Fact]
		public static void EnumWithDifferentBaseType()
		{
			AssertCloneMatchesOriginal(ByteEnum.Value2);
		}

		[Fact]
		public static void NullableInt32()
		{
			AssertCloneMatchesOriginal((int?)32);
		}

		[Fact]
		public static void NullableInt32WhenNull()
		{
			AssertCloneMatchesOriginal((int?)null);
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
		public static void Char()
		{
			AssertCloneMatchesOriginal('é');
		}

		[Fact]
		public static void String()
		{
			AssertCloneMatchesOriginal("Café");
		}

		[Fact]
		public static void NullArrayOfInt32()
		{
			AssertCloneMatchesOriginal((int[])null);
		}

		[Fact]
		public static void EmptyArrayOfInt32()
		{
			AssertCloneMatchesOriginal(new int[0]);
		}

		[Fact]
		public static void ArrayOfInt32()
		{
			AssertCloneMatchesOriginal(new[] { 32 });
		}

		[Fact]
		public static void NullListOfInt32()
		{
			AssertCloneMatchesOriginal((List<int>)null);
		}

		[Fact]
		public static void EmptyListOfInt32()
		{
			AssertCloneMatchesOriginal(new List<int>());
		}

		[Fact]
		public static void ListOfInt32()
		{
			AssertCloneMatchesOriginal(new List<int> { 32 });
		}

		[Fact]
		public static void PrivateSealedClassWithNoMembers()
		{
			var clone = Clone(new ClassWithNoMembersAndNoInheritance());
			Assert.NotNull(clone);
			Assert.IsType<ClassWithNoMembersAndNoInheritance>(clone);
		}

		[Fact]
		public static void NullPrivateSealedClassWithNoMembers()
		{
			var clone = Clone((ClassWithNoMembersAndNoInheritance)null);
			Assert.Null(clone);
		}

		[Fact]
		public static void PrivateSealedClassWithSinglePublicField()
		{
			var clone = Clone(new ClassWithSinglePublicFieldAndNoInheritance { Name = "abc" });
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicFieldAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public static void PrivateSealedClassWithSinglePublicAutoProperty()
		{
			var clone = Clone(new ClassWithSinglePublicAutoPropertyAndNoInheritance { Name = "abc" });
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public static void PrivateSealedClassWithSinglePublicReadonlyAutoProperty()
		{
			var clone = Clone(new ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance("abc"));
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This specifies the target type as an interface but the data that will be serialised and deserialised is an implementation of that interface
		/// </summary>
		[Fact]
		public static void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToInterface()
		{
			var clone = Clone<IHaveName>(new ClassWithSinglePublicAutoPropertyToImplementAnInterfaceButNoInheritance { Name = "abc" });
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyToImplementAnInterfaceButNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This is basically the same as above but the class implements the interface explicitly
		/// </summary>
		[Fact]
		public static void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToInterfaceThatIsExplicitlyImplemented()
		{
			var clone = Clone<IHaveName>(new ClassWithSinglePublicAutoPropertyToExplicitlyImplementAnInterfaceButNoInheritance { Name = "abc" });
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyToExplicitlyImplementAnInterfaceButNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This specifies the target type as an abstract class but the data that will be serialised and deserialised is an implementation of that interface (this test confirms
		/// not only will the type be maintained but that a property will be set on the base class class AND one defined on the derived type)
		/// </summary>
		[Fact]
		public static void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToAbstractClass()
		{
			var clone = Clone<NamedItem>(new ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance { Name = "abc", OtherProperty = "xyz" });
			Assert.NotNull(clone);
			Assert.IsType<ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance>(clone);
			Assert.Equal("abc", clone.Name);
			Assert.Equal("xyz", ((ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance)clone).OtherProperty);
		}

		[Fact]
		public static void PropertyOnBaseClassThatIsOverriddenOnDerivedClass()
		{
			var source = new SupervisorDetails(123, "abc");
			var clone = Clone(new SupervisorDetails(123, "abc"));
			Assert.IsType<SupervisorDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public static void PropertyOnBaseClassThatIsOverriddenWithNewOnDerivedClass()
		{
			var source = new ManagerDetails(123, "abc");
			var clone = Clone(new ManagerDetails(123, "abc"));
			Assert.IsType<ManagerDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", ((EmployeeDetails)clone).Name);
		}

		[Fact]
		public static void PrivateStructWithNoMembers()
		{
			var clone = Clone(new StructWithNoMembers());
			Assert.IsType<StructWithNoMembers>(clone);
		}

		[Fact]
		public static void PrivateStructWithSinglePublicField()
		{
			var clone = Clone(new StructWithSinglePublicField { Name = "abc" });
			Assert.IsType<StructWithSinglePublicField>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public static void PrivateStructWithSinglePublicAutoProperty()
		{
			var clone = Clone(new StructWithSinglePublicAutoProperty { Name = "abc" });
			Assert.IsType<StructWithSinglePublicAutoProperty>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public static void CircularReferenceThrows()
		{
			var source = new Node();
			source.Child = source;
			Assert.Throws<CircularReferenceException>(() =>
			{
				var writer = new BinaryWriter();
				Serialiser.Instance.Serialise(source, writer);
			});
		}

		[Fact]
		public static void StaticDataIsNotSerialised()
		{
			// Only instance fields will be serialised, which means that any static fields / properties will be unaffected by the deserialisation process. To illustrate this..
			// - Create something to clone that has a static property and set that property to a known value
			var source = new ClassWithStaticProperty();
			ClassWithStaticProperty.Count = 1;
			// - Serialise that data (if static fields were going to be serialised then the value of the static field would be captured here)
			var writer = new BinaryWriter();
			Serialiser.Instance.Serialise(source, writer);
			var serialisedData = writer.GetData();
			// - Change the static property to a different value
			ClassWithStaticProperty.Count = 2;
			// - Deserialise.. if this were to read a value for the static property from the serialised data and set it then the static property value would revert back to
			//   the value that it had when it was serialised
			var reader = new BinaryReader(serialisedData);
			var clone = reader.Read<ClassWithStaticProperty>();
			// - Confirm that the static property was NOT reverted back to the value that it had when the data was serialised
			Assert.Equal(2, ClassWithStaticProperty.Count);
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

		private sealed class ClassWithSinglePublicFieldAndNoInheritance
		{
			public string Name;
		}

		private sealed class ClassWithSinglePublicAutoPropertyAndNoInheritance
		{
			public string Name { get; set; }
		}

		private sealed class ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance
		{
			public ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance(string name)
			{
				Name = name;
			}

			public string Name { get; }
		}

		private sealed class ClassWithSinglePublicAutoPropertyToImplementAnInterfaceButNoInheritance : IHaveName
		{
			public string Name { get; set; }
		}

		private sealed class ClassWithSinglePublicAutoPropertyToExplicitlyImplementAnInterfaceButNoInheritance : IHaveName
		{
			public string Name { get; set; }

			string IHaveName.Name { get { return Name; } }
		}

		private interface IHaveName
		{
			string Name { get; }
		}

		private sealed class ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance : NamedItem
		{
			public string OtherProperty { get; set; }
		}

		private abstract class NamedItem
		{
			public string Name { get; set; }
		}

		private class SupervisorDetails : EmployeeDetails
		{
			public SupervisorDetails(int id, string name) : base(id, name) { }
			public override string Name { get; protected set; }
		}

		private class ManagerDetails : EmployeeDetails
		{
			public ManagerDetails(int id, string name) : base(id, name) { }
			public new string Name { get; }
		}

		private class EmployeeDetails
		{
			public EmployeeDetails(int id, string name)
			{
				Id = id;
				Name = name;
			}
			public int Id { get; }
			public virtual string Name { get; protected set; }
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}

		private class ClassWithStaticProperty
		{
			public static int Count { get; set; }
		}

		private struct StructWithNoMembers { }

		private struct StructWithSinglePublicField
		{
			public string Name;
		}

		private struct StructWithSinglePublicAutoProperty
		{
			public string Name { get; set; }
		}

		private enum DefaultTypeEnum { Value1, Value2 }

		private enum ByteEnum : byte { Value1, Value2 }
	}
}