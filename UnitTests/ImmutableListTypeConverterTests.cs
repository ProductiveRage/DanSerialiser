using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class ImmutableListTypeConverterTests
	{
		public static class SystemCollectionsGenericListTests
		{
			[Fact]
			public static void DoNotTryToChange()
			{
				var value = new List<string> { "One", "Two" };
				Assert.Same(
					value,
					((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value)
				);
			}
		}

		public sealed class MicrosoftImmutableList : SharedTestsForApplicableListTypes
		{
			protected override Type GetListType<T>() => typeof(ImmutableList<T>);
			protected override IEnumerable<T> GetList<T>(T[] valuesIfAny) => (valuesIfAny == null) ? null : ImmutableList.Create(valuesIfAny);
		}

		/// <summary>
		/// The PersistentList class has its own ToArray instance method, which makes it slightly different to the ImmutableList (the type converter will use an instance method
		/// if there is one) and it has an Empty property (whereas ImmutableList has a field) and an InsertRange method (where ImmutableList has an AddRange method)
		/// </summary>
		public sealed class CustomPersistentList : SharedTestsForApplicableListTypes
		{
			protected override Type GetListType<T>() => typeof(PersistentList<T>);
			protected override IEnumerable<T> GetList<T>(T[] valuesIfAny) => (valuesIfAny == null) ? null : PersistentList.Of(valuesIfAny);
		}

		public abstract class SharedTestsForApplicableListTypes
		{
			[Fact]
			public void NullImmutableListOfStringSerialisedAsNull()
			{
				var value = Convert.ChangeType(null, GetListType<string>());
				var serialised = ((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value);
				AssertEqualContentsAndThatTypesMatch(null, serialised);

				var deserialised = ((IDeserialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(
					GetListType<string>(),
					serialised
				);
				AssertEqualContentsAndThatTypesMatch(value, deserialised);
			}

			[Fact]
			public void EmptyImmutableListOfStringSerialisedViaStringArray()
			{
				var value = GetList<string>();
				var serialised = ((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value);
				AssertEqualContentsAndThatTypesMatch(value.ToArray(), serialised);

				var deserialised = ((IDeserialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(
					GetListType<string>(),
					serialised
				);
				AssertEqualContentsAndThatTypesMatch(value, deserialised);
			}

			[Fact]
			public void ImmutableListOfStringSerialisedViaStringArray()
			{
				var value = GetList("One", "Two");
				var serialised = ((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value);
				AssertEqualContentsAndThatTypesMatch(value.ToArray(), serialised);

				var deserialised = ((IDeserialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(
					GetListType<string>(),
					serialised
				);
				AssertEqualContentsAndThatTypesMatch(value, deserialised);
			}

			protected abstract Type GetListType<T>();
			protected abstract IEnumerable<T> GetList<T>(params T[] valuesIfAny);
		}

		private static void AssertEqualContentsAndThatTypesMatch(object expected, object actual)
		{
			if (expected == null)
			{
				Assert.Null(actual);
				return;
			}
			else
				Assert.NotNull(actual);

			Assert.IsType(expected.GetType(), actual);
			Assert.Equal(expected, actual); // For collection types, this compares their contents but not they type (hence the code above)
		}
	}
}