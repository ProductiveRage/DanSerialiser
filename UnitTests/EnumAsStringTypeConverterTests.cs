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

		/// <summary>
		/// This would happen if an invalid value is cast to the enum or if there are combined enum flag values (which are not supported) - the value will be written as a blank
		/// string, which will be interpreted as the default enum value when deserialised (see UnrecognisedValueWillBeDeserialisedIntoEnumDefault)
		/// </summary>
		[Fact]
		public static void UnrecognisedValueWillBeSerialisedAsBlankString()
		{
			Assert.Equal(
				"",
				((ISerialisationTypeConverter)EnumAsStringTypeConverter.Instance).ConvertIfRequired((DayOfWeek)99)
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