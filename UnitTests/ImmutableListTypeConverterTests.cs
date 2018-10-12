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
		public sealed class MicrosoftImmutableList : SharedTests
		{
			protected override Type GetListType<T>() => typeof(ImmutableList<T>);
			protected override IEnumerable<T> GetList<T>(T[] valuesIfAny) => (valuesIfAny == null) ? null : ImmutableList.Create(valuesIfAny);
		}

		public sealed class CustomPersistentList : SharedTests
		{
			protected override Type GetListType<T>() => typeof(PersistentList<T>);
			protected override IEnumerable<T> GetList<T>(T[] valuesIfAny) => (valuesIfAny == null) ? null : PersistentList.Of(valuesIfAny);
		}

		public abstract class SharedTests
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