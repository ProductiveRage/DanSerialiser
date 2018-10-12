using System.Collections.Immutable;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class ImmutableListTypeConverterTests
	{
		public static class MicrosoftImmutableList
		{
			[Fact]
			public static void NullImmutableListOfStringSerialisedAsNull()
			{
				ImmutableList<string> value = null;
				var serialised = ((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value);
				AssertEqualContentsAndThatTypesMatch(null, serialised);

				var deserialised = ((IDeserialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(
					typeof(ImmutableList<string>),
					serialised
				);
				AssertEqualContentsAndThatTypesMatch(value, deserialised);
			}

			[Fact]
			public static void ImmutableListOfStringSerialisedViaStringArray()
			{
				var value = ImmutableList.Create("One", "Two");
				var serialised = ((ISerialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(value);
				AssertEqualContentsAndThatTypesMatch(new[] { "One", "Two" }, serialised);

				var deserialised = ((IDeserialisationTypeConverter)ImmutableListTypeConverter.Instance).ConvertIfRequired(
					typeof(ImmutableList<string>),
					serialised
				);
				AssertEqualContentsAndThatTypesMatch(value, deserialised);
			}
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