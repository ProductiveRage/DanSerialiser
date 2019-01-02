using System;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class TypeTypeConverterTests
	{
		[Fact]
		public static void RoundTripOfStringType()
		{
			var value = typeof(string);
			var convertedValue = ((ISerialisationTypeConverter)TypeTypeConverter.Instance).ConvertIfRequired(value);
			Assert.IsType<string>(convertedValue);
			var convertedBackValue = ((IDeserialisationTypeConverter)TypeTypeConverter.Instance).ConvertIfRequired(typeof(Type), value);
			Assert.Equal(value, convertedBackValue);
		}

		[Fact]
		public static void RoundTripOfStringTypeUsingFastSerialisation()
		{
			var value = typeof(string);
			var typeConverters = new[] { TypeTypeConverter.Instance };
			var serialised = FastestTreeBinarySerialisation.GetSerialiser(typeConverters).Serialise(value);
			var clone = BinarySerialisation.Deserialise<Type>(serialised, typeConverters);
			Assert.Equal(value, clone);
		}
	}
}