using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DanSerialiser;
using DanSerialiser.CachedLookups;
using DanSerialiser.Reflection;
using Xunit;

namespace UnitTests
{
	public sealed class BinarySerialisationTests_SpeedyButLimited : BinarySerialisationTests
	{
		public BinarySerialisationTests_SpeedyButLimited() : base(ReferenceReuseOptions.SpeedyButLimited) { }

		// The "SpeedyButLimited" option doesn't detect OR handle circular references - so there are no tests for them because the only test would be to see if we get a stack overflow
		// and the xunit runner won't handle that (it will terminate the process when the stack overflow occurs and then the test will be marked neither as a pass or a fail)

		/// <summary>
		/// I thought that I'd seen a problem where the SpeedyButLimited's PrepareForSerialisation call would return nothing if the target type was an array.. but this test shows that
		/// that isn't the case and I must have misremembered!
		/// </summary>
		[Fact]
		public void EnsureThatMemberSetterPreparedForElementTypeWhenTargetTypeIsArray()
		{
			using (var stream = new MemoryStream())
			{
				var writer = new BinarySerialisationWriter(
					stream,
					ReferenceReuseOptions.SpeedyButLimited,
					DefaultTypeAnalyser.Instance,
					new ConcurrentDictionary<Type, BinarySerialisationDeepCompiledMemberSetters.DeepCompiledMemberSettersGenerationResults>()
				);
				var generatedMemberSetters = writer.PrepareForSerialisation(typeof(SealedClassWithSingleStringProperty[]), new IFastSerialisationTypeConverter[0]);
				Assert.NotNull(generatedMemberSetters); // Should never get a null response back 
				Assert.Single(generatedMemberSetters); // In this case, there should be a single entry that is a non-null member setter for SealedClassWithSingleStringProperty (NOT array of)
				Assert.Equal(typeof(SealedClassWithSingleStringProperty), generatedMemberSetters.First().Key);
				Assert.NotNull(generatedMemberSetters.First().Value);
			}
		}

		private sealed class SealedClassWithSingleStringProperty
		{
			public string Name { get; set; }
		}
	}

	public sealed class BinarySerialisationTests_NoReferenceReuse : BinarySerialisationTests
	{
		public BinarySerialisationTests_NoReferenceReuse() : base(ReferenceReuseOptions.NoReferenceReuse) { }

		[Fact]
		public void CircularReferenceThrows()
		{
			var source = new Node();
			source.Child = source;
			Assert.Throws<CircularReferenceException>(() =>
			{
				BinarySerialisation.Serialise(source, new ISerialisationTypeConverter[0], _referenceReuseStrategy);
			});
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}
	}

	public sealed class BinarySerialisationTests_SupportReferenceReUseInMostlyTreeLikeStructure : BinarySerialisationTests
	{
		public BinarySerialisationTests_SupportReferenceReUseInMostlyTreeLikeStructure() : base(referenceReuseStrategy: ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure) { }

		[Fact]
		public void CircularReferenceSupported()
		{
			var source = new Node();
			source.Child = source;

			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.Equal(clone, clone.Child);
		}

		/// <summary>
		/// The BinarySerialisationReader has an optimisation that it applies when the same type is encountered multiple times (it performs less reflection / validation on the
		/// serialised fields) but this was causing a problem if there were circular references within the type because the new instances were being constructed quickly but not
		/// being added to the shared-object-references lookup. This test was failing before but will pass with the fix included in the changeset with it.
		/// </summary>
		[Fact]
		public void CircularReferencesAreSupportedWhereTheSameTypeIsEncounteredMultipleTimes()
		{
			var source = new List<Node>();
			var node = new Node();
			node.Child = node;
			source.Add(node);
			node = new Node();
			node.Child = node;
			source.Add(node);

			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.Equal(2, clone.Count);
			Assert.Equal(clone[0], clone[0].Child);
			Assert.Equal(clone[1], clone[1].Child);
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}
	}

	public sealed class BinarySerialisationTests_OptimiseForWideCircularReferences : BinarySerialisationTests
	{
		public BinarySerialisationTests_OptimiseForWideCircularReferences() : base(referenceReuseStrategy: ReferenceReuseOptions.OptimiseForWideCircularReferences) { }

		[Fact]
		public void CircularReferenceSupported()
		{
			var source = new Node();
			source.Child = source;

			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.Equal(clone, clone.Child);
		}

		[Fact]
		public void WideArrayCircularReferencesDoNotThrow()
		{
			var categories = Enumerable.Range(0, 1000).Select(i => new Category { Key = 100000 + i }).ToDictionary(c => c.Key, c => c);
			var categoryGroups = Enumerable.Range(0, 1000).Select(i => new CategoryGroup { Key = 900000 + i, Categories = categories }).ToDictionary(g => g.Key, g => g);
			foreach (var category in categories.Values)
				category.Groups = categoryGroups;

			BinarySerialisationCloner.Clone(categories, _referenceReuseStrategy);
			Assert.True(true);
		}

		private sealed class Node
		{
			public Node Child { get; set; }
		}

		private class Category
		{
			public int Key { get; set; }
			public Dictionary<int, CategoryGroup> Groups { get; set; }
		}

		private class CategoryGroup
		{
			public int Key { get; set; }
			public Dictionary<int, Category> Categories { get; set; }
		}
	}

	public abstract class BinarySerialisationTests
	{
		protected readonly ReferenceReuseOptions _referenceReuseStrategy;
		protected BinarySerialisationTests(ReferenceReuseOptions referenceReuseStrategy)
		{
			_referenceReuseStrategy = referenceReuseStrategy;
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

		/// <summary>
		/// There was a bug in the DoubleBytes implementation that the test above didn't pick up - this is the value that made me realise
		/// </summary>
		[Fact]
		public void DifferentDouble()
		{
			AssertCloneMatchesOriginal(63.85255);
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
		public void DateTime() => AssertCloneMatchesOriginal(new DateTime(2018, 9, 26, 23, 11, 59, DateTimeKind.Local));

		[Fact]
		public void TimeSpan() => AssertCloneMatchesOriginal(new TimeSpan(26, 23, 12, 15, 123));

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
		public void ArrayOfNullableInt32()
		{
			AssertCloneMatchesOriginal(new int?[] { 123, 456, null });
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

		/// <summary>
		/// This tests a fix that I made for serialisation with ReferenceReuseOptions.NoReferenceReuse configuration - the BCL generic Dictionary has internals 'keys' and 'values' fields
		/// that are instances of classes that have a reference back to the dictionary and so there will be a circular reference. However, those fields are null until the public 'Keys' or
		/// 'Values' properties are accessed and so the serialisation behaviour varied depending upon whether or not they had been accessed before attempting serialisation. For configurations
		/// that supported reference reuse, there would be no exception but a little extra serialisation and deserialisation work was required before the change (to skip the 'keys' and 'values'
		/// fields when serialising).
		/// </summary>
		[Fact]
		public void DictionaryOfInt32ToStringWhereKeysPropertyAccessedBeforeClone()
		{
			var source = new Dictionary<string, int>() { { "One", 1 } };
			Console.WriteLine(source.Keys);
			var clone = AssertCloneMatchesOriginalAndReturnClone(source);
			Assert.Single(clone);
			Assert.True(clone.ContainsKey("One"));
		}

		/// <summary>
		/// Variation on DictionaryOfInt32ToStringWhereKeysPropertyAccessedBeforeClone
		/// </summary>
		[Fact]
		public void DictionaryOfInt32ToStringWhereValuesPropertyAccessedBeforeClone()
		{
			var source = new Dictionary<string, int>() { { "One", 1 } };
			Console.WriteLine(source.Values);
			var clone = AssertCloneMatchesOriginalAndReturnClone(source);
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
			var clone = BinarySerialisationCloner.Clone(new ClassWithNoMembersAndNoInheritance(), _referenceReuseStrategy);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithNoMembersAndNoInheritance>(clone);
		}

		[Fact]
		public void NullPrivateSealedClassWithNoMembers()
		{
			var clone = BinarySerialisationCloner.Clone((ClassWithNoMembersAndNoInheritance)null, _referenceReuseStrategy);
			Assert.Null(clone);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicField()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicFieldAndNoInheritance { Name = "abc" }, _referenceReuseStrategy);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicFieldAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicAutoPropertyAndNoInheritance { Name = "abc" }, _referenceReuseStrategy);
			Assert.NotNull(clone);
			Assert.IsType<ClassWithSinglePublicAutoPropertyAndNoInheritance>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateSealedClassWithSinglePublicReadonlyAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new ClassWithSinglePublicReadonlyAutoPropertyAndNoInheritance("abc"), _referenceReuseStrategy);
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
				_referenceReuseStrategy
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
				_referenceReuseStrategy
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
				_referenceReuseStrategy
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
			var clone = BinarySerialisationCloner.Clone(new SupervisorDetails(123, "abc"), _referenceReuseStrategy);
			Assert.IsType<SupervisorDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PropertyOnBaseClassThatIsOverriddenWithNewOnDerivedClass()
		{
			var source = new ManagerDetails(123, "abc");
			var clone = BinarySerialisationCloner.Clone(new ManagerDetails(123, "abc"), _referenceReuseStrategy);
			Assert.IsType<ManagerDetails>(clone);
			Assert.Equal(123, clone.Id);
			Assert.Equal("abc", ((EmployeeDetails)clone).Name);
		}

		[Fact]
		public void PrivateStructWithNoMembers()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithNoMembers(), _referenceReuseStrategy);
			Assert.IsType<StructWithNoMembers>(clone);
		}

		[Fact]
		public void PrivateStructWithSinglePublicField()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithSinglePublicField { Name = "abc" }, _referenceReuseStrategy);
			Assert.IsType<StructWithSinglePublicField>(clone);
			Assert.Equal("abc", clone.Name);
		}

		[Fact]
		public void PrivateStructWithSinglePublicAutoProperty()
		{
			var clone = BinarySerialisationCloner.Clone(new StructWithSinglePublicAutoProperty { Name = "abc" }, _referenceReuseStrategy);
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
			var serialisedData = BinarySerialisation.Serialise(source, new ISerialisationTypeConverter[0], _referenceReuseStrategy);
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
			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.Equal(0, clone.Id);
		}

		[Fact]
		public void NonSerializedPropertydNotSerialised()
		{
			var source = new SomethingWithNonSerialisableIdProperty { Id = 123 };
			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.Equal(0, clone.Id);
		}

		/// <summary>
		/// This confirms a fix for a bug around the 'optimised member setter' logic used when serialising arrays - we need to be careful to look for a member setter that
		/// corresponds to the type of the element and not the type of the array element (because the actual instance may be a more specific type than the array and we
		/// want to ensure that we set any additional fields - or that we don't use a quick-member-setter if it's not applicable to the more specific type)
		/// </summary>
		[Fact]
		public void MemberSetterCacheTargetsMostSpecificTypeForArrayElementsWhereMostSpecificTypeIsNotEligibleForOptimisedMemberSetter()
		{
			var source = new EmptyThingBase[]
			{
				new ThingWithWrappedStringName(new WrappedString("abc")),
				new ThingWithWrappedStringName(new WrappedString("xyz"))
			};
			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.NotNull(clone);
			Assert.Equal(2, clone.Length);
			Assert.IsType<ThingWithWrappedStringName>(clone[0]);
			Assert.Equal("abc", ((ThingWithWrappedStringName)clone[0]).Name?.Value);
			Assert.IsType<ThingWithWrappedStringName>(clone[1]);
			Assert.Equal("xyz", ((ThingWithWrappedStringName)clone[1]).Name?.Value);
		}

		/// <summary>
		/// While investigating the fix that the test above confirms, I found that there was a variation on it - the test above is for the case where the array element type
		/// is one that the BinarySerialisationCompiledMemberSetters is able to produce an 'optimised member setter' for but the specialised type that each element of the
		/// array actually consists of is NOT one that a member setter can be produced for. This test covers the case where the specialised type IS one that a member setter
		/// may be prepared for.
		/// </summary>
		[Fact]
		public void MemberSetterCacheTargetsMostSpecificTypeForArrayElementsWhereMostSpecificTypeIsEligibleForOptimisedMemberSetter()
		{
			var source = new EmptyThingBase[]
			{
				new ThingWithWrappedStringName(new WrappedString("abc")),
				new ThingWithWrappedStringName(new WrappedString("xyz"))
			};
			var clone = BinarySerialisationCloner.Clone(source, _referenceReuseStrategy);
			Assert.NotNull(clone);
			Assert.Equal(2, clone.Length);
			Assert.IsType<ThingWithWrappedStringName>(clone[0]);
			Assert.Equal("abc", ((ThingWithWrappedStringName)clone[0]).Name?.Value);
			Assert.IsType<ThingWithWrappedStringName>(clone[1]);
			Assert.Equal("xyz", ((ThingWithWrappedStringName)clone[1]).Name?.Value);
		}

		private T AssertCloneMatchesOriginalAndReturnClone<T>(T value)
		{
			var clone = BinarySerialisationCloner.Clone(value, _referenceReuseStrategy);
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

		public sealed class WrappedString
		{
			public WrappedString(string value) => Value = value;
			public string Value { get; }
		}

		private sealed class ThingWithStringName : EmptyThingBase
		{
			public ThingWithStringName(string name) => Name = name;
			public string Name { get; }
		}

		private sealed class ThingWithWrappedStringName : EmptyThingBase
		{
			public ThingWithWrappedStringName(WrappedString name) => Name = name;
			public WrappedString Name { get; }
		}

		private abstract class EmptyThingBase { }
	}
}