using System;
using System.Collections.Generic;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public sealed class BinarySerialisationTests_DisallowReferenceReuse : BinarySerialisationTests
	{
		public BinarySerialisationTests_DisallowReferenceReuse() : base(supportReferenceReuse: false) { }

		[Fact]
		public void CircularReferenceThrows()
		{
			var source = new Node();
			source.Child = source;
			Assert.Throws<CircularReferenceException>(() =>
			{
				BinarySerialisation.Serialise(source, supportReferenceReuse: false);
			});
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}
	}

	public sealed class BinarySerialisationTests_AllowReferenceReuse : BinarySerialisationTests
	{
		public BinarySerialisationTests_AllowReferenceReuse() : base(supportReferenceReuse: true) { }

		[Fact]
		public void CircularReferenceSupported()
		{
			var source = new Node();
			source.Child = source;

			var clone = BinarySerialisationCloner.Clone(source, supportReferenceReuse: true);
			Assert.Equal(clone, clone.Child);
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}
	}

	public abstract class BinarySerialisationTests
	{
		private readonly bool _supportReferenceReuse;
		protected BinarySerialisationTests(bool supportReferenceReuse)
		{
			_supportReferenceReuse = supportReferenceReuse;
		}

		[Fact]
		public void Bool()
		{
			AssertCloneMatchesOriginal(true);
		}

		[Fact]
		public void Byte()
		{
			AssertCloneMatchesOriginal(byte.MaxValue);
		}

		[Fact]
		public void SByte()
		{
			AssertCloneMatchesOriginal(sbyte.MaxValue);
		}

		[Fact]
		public void Int16()
		{
			AssertCloneMatchesOriginal(short.MaxValue);
		}

		[Fact]
		public void Int32()
		{
			AssertCloneMatchesOriginal(int.MaxValue);
		}

		[Fact]
		public void Int64()
		{
			AssertCloneMatchesOriginal(long.MaxValue);
		}
		[Fact]
		public void UInt16()
		{
			AssertCloneMatchesOriginal(ushort.MaxValue);
		}

		[Fact]
		public void UInt32()
		{
			AssertCloneMatchesOriginal(uint.MaxValue);
		}

		[Fact]
		public void UInt64()
		{
			AssertCloneMatchesOriginal(ulong.MaxValue);
		}

		[Fact]
		public void Single()
		{
			AssertCloneMatchesOriginal(float.MaxValue);
		}

		[Fact]
		public void Double()
		{
			AssertCloneMatchesOriginal(double.MaxValue);
		}

		[Fact]
		public void Decimal()
		{
			AssertCloneMatchesOriginal(decimal.MaxValue);
		}

		[Fact]
		public void Enum()
		{
			AssertCloneMatchesOriginal(DefaultTypeEnum.Value2);
		}

		[Fact]
		public void EnumWithDifferentBaseType()
		{
			AssertCloneMatchesOriginal(ByteEnum.Value2);
		}

		[Fact]
		public void NullableInt32()
		{
			AssertCloneMatchesOriginal((int?)32);
		}

		[Fact]
		public void NullableInt32WhenNull()
		{
			AssertCloneMatchesOriginal((int?)null);
		}

		[Fact]
		public void NullString()
		{
			AssertCloneMatchesOriginal((string)null);
		}

		[Fact]
		public void Char()
		{
			AssertCloneMatchesOriginal('é');
		}

		[Fact]
		public void BlankString()
		{
			AssertCloneMatchesOriginal("");
		}

		[Fact]
		public void String()
		{
			AssertCloneMatchesOriginal("Café");
		}

		[Fact]
		public void Guid()
		{
			AssertCloneMatchesOriginal(GenerateSeededGuid(0));
		}

		private Guid GenerateSeededGuid(int seed) // Courtesy of https://stackoverflow.com/a/13188409/3813189
		{
			var r = new Random(seed);
			var guid = new byte[16];
			r.NextBytes(guid);
			return new Guid(guid);
		}

		[Fact]
		public void NullArrayOfInt32()
		{
			AssertCloneMatchesOriginal((int[])null);
		}

		[Fact]
		public void EmptyArrayOfInt32()
		{
			AssertCloneMatchesOriginal(new int[0]);
		}

		[Fact]
		public void ArrayOfInt32()
		{
			AssertCloneMatchesOriginal(new[] { 32 });
		}

		[Fact]
		public void NullListOfInt32()
		{
			AssertCloneMatchesOriginal((List<int>)null);
		}

		[Fact]
		public void EmptyListOfInt32()
		{
			AssertCloneMatchesOriginal(new List<int>());
		}

		[Fact]
		public void ListOfInt32()
		{
			AssertCloneMatchesOriginal(new List<int> { 32 });
		}

		[Fact]
		public void NullDictionaryOfInt32ToString()
		{
			AssertCloneMatchesOriginal((Dictionary<string, int>)null);
		}

		[Fact]
		public void DictionaryOfInt32ToString()
		{
			var clone = AssertCloneMatchesOriginalAndReturnClone(new Dictionary<string, int>() { { "One", 1 } });
			Assert.Single(clone);
			Assert.True(clone.ContainsKey("One"));
		}

		[Fact]
		public void DictionaryOfInt32ToStringWithSpecificComparer()
		{
			var clone = AssertCloneMatchesOriginalAndReturnClone(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "One", 1 } });
			Assert.Single(clone);
			Assert.True(clone.ContainsKey("ONE"));
		}

		[Fact]
		public void NullObject()
		{
			AssertCloneMatchesOriginal<object>(null);
		}

		[Fact]
		public void PrivateSealedClassWithNoMembers()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithNoMembersAndNoInheritance(), _supportReferenceReuse);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithNoMembersAndNoInheritance>(clone);
		}

		[Fact]
		public void NullPrivateSealedClassWithNoMembers()
		{
			var clone = BinarySerialisationCloner.Clone((ClassWithNoMembersAndNoInheritance)null, _supportReferenceReuse);
			Assert.Null(clone);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicField()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicFieldAndNoInheritance { Name = "abc" }, _supportReferenceReuse);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicFieldAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicAutoPropertyAndNoInheritance { Name = "abc" }, _supportReferenceReuse);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicReadonlyAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance("abc"), _supportReferenceReuse);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This specifies the target type as an interface but the data that will be serialised and deserialised is an implementation of that interface
		/// </summary>
		[Fact]
		public void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToInterface()
		{
			var clone = BinarySerialisationCloner.Clone<IHaveName>(
				new ClassWithSinglePublicAutoPropertyToImplementAnInterfaceButNoInheritance { Name = "abc" },
				_supportReferenceReuse
			);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyToImplementAnInterfaceButNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This is basically the same as above but the class implements the interface explicitly
		/// </summary>
		[Fact]
		public void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToInterfaceThatIsExplicitlyImplemented()
		{
			var clone = BinarySerialisationCloner.Clone<IHaveName>(
				new ClassWithSinglePublicAutoPropertyToExplicitlyImplementAnInterfaceButNoInheritance { Name = "abc" },
				_supportReferenceReuse
			);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyToExplicitlyImplementAnInterfaceButNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		/// <summary>
		/// This specifies the target type as an abstract class but the data that will be serialised and deserialised is an implementation of that interface (this test confirms
		/// not only will the type be maintained but that a property will be set on the base class class AND one defined on the derived type)
		/// </summary>
		[Fact]
		public void PrivateSealedClassWithSinglePublicReadonlyAutoPropertyThatIsSerialisedToAbstractClass()
		{
			var clone = BinarySerialisationCloner.Clone<NamedItem>(
				new ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance { Name = "abc", OtherProperty = "xyz" },
				_supportReferenceReuse
			);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance>(clone);
			Assert.Equal("abc", clone.Name);
			Assert.Equal("xyz", ((ClassWithOwnPublicAutoPropertyAndPublicAutoPropertyInheritedFromAnAbstractClassButNoOtherInheritance)clone).OtherProperty);
		}

		[Fact]
		public void PropertyOnBaseClassThatIsOverriddenOnDerivedClass()
		{
			var source = new SupervisorDetails(123, "abc");
			var clone = BinarySerialisationCloner.Clone(new SupervisorDetails(123, "abc"), _supportReferenceReuse);
			Assert.IsType<SupervisorDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PropertyOnBaseClassThatIsOverriddenWithNewOnDerivedClass()
		{
			var source = new ManagerDetails(123, "abc");
			var clone = BinarySerialisationCloner.Clone(new ManagerDetails(123, "abc"), _supportReferenceReuse);
			Assert.IsType<ManagerDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", ((EmployeeDetails)clone).Name);
		}

		[Fact]
		public void PrivateStructWithNoMembers()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithNoMembers(), _supportReferenceReuse);
			Assert.IsType<StructWithNoMembers>(clone);
		}

		[Fact]
		public void PrivateStructWithSinglePublicField()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithSinglePublicField { Name = "abc" }, _supportReferenceReuse);
			Assert.IsType<StructWithSinglePublicField>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateStructWithSinglePublicAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithSinglePublicAutoProperty { Name = "abc" }, _supportReferenceReuse);
			Assert.IsType<StructWithSinglePublicAutoProperty>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void StaticDataIsNotSerialised()
		{
			// Only instance fields will be serialised, which means that any fields / properties will be unaffected by the deserialisation process. To illustrate this..
			// - Create something to clone that has a property and set that property to a known value
			var source = new ClassWithStaticProperty();
			ClassWithStaticProperty.Count = 1;
			// - Serialise that data (if fields were going to be serialised then the value of the field would be captured here)
			var serialisedData = BinarySerialisation.Serialise(source, _supportReferenceReuse);
			// - Change the property to a different value
			ClassWithStaticProperty.Count = 2;
			// - Deserialise.. if this were to read a value for the property from the serialised data and set it then the property value would revert back to
			//   the value that it had when it was serialised
			var clone = BinarySerialisation.Deserialise<ClassWithStaticProperty>(serialisedData);
			// - Confirm that the property was NOT reverted back to the value that it had when the data was serialised
			Assert.Equal(2, ClassWithStaticProperty.Count);
		}

		[Fact]
		public void NonSerializedFieldNotSerialised()
		{
			var source = new SomethingWithNonSerialisableIdField { Id = 123 };
			var clone = BinarySerialisationCloner.Clone(source, _supportReferenceReuse);
			Assert.Equal(0, clone.Id);
		}

		[Fact]
		public void NonSerializedPropertydNotSerialised()
		{
			var source = new SomethingWithNonSerialisableIdProperty { Id = 123 };
			var clone = BinarySerialisationCloner.Clone(source, _supportReferenceReuse);
			Assert.Equal(0, clone.Id);
		}

		private T AssertCloneMatchesOriginalAndReturnClone<T>(T value)
		{
			var clone = BinarySerialisationCloner.Clone(value, _supportReferenceReuse);
			Assert.Equal(value, clone);
			return clone;
		}

		private void AssertCloneMatchesOriginal<T>(T value)
		{
			AssertCloneMatchesOriginalAndReturnClone(value);
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

		private class ClassWithStaticProperty
		{
			public static int Count { get; set; }
		}

		private sealed class SomethingWithNonSerialisableIdField
		{
			[NonSerialized]
			public int Id;
		}

		private sealed class SomethingWithNonSerialisableIdProperty
		{
			[field:NonSerialized]
			public int Id { get; set; }
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