using System;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class EnumAsStringTypeConverterTests
	{
		[Fact]
		public static void UnrecognisedValueWillBeDeserialisedIntoEnumDefault()
		{
			Assert.Equal(
				(DayOfWeek)0,
				((IDeserialisationTypeConverter)EnumAsStringTypeConverter.Instance).ConvertIfRequired(
					typeof(DayOfWeek),
					"Unknown"
				)
			);
		}

		[Fact]
		public static void RoundTripOfNonZeroEnumValueWorks()
		{
			var value = DayOfWeek.Monday; // Monday has value 1
			var serialised = ((ISerialisationTypeConverter)EnumAsStringTypeConverter.Instance).ConvertIfRequired(value);
			Assert.Equal(
				value,
				((IDeserialisationTypeConverter)EnumAsStringTypeConverter.Instance).ConvertIfRequired(
					value.GetType(),
					serialised
				)
			);
		}
	}
}