using DanSerialiser;

namespace UnitTests
{
	internal static class BinarySerialisationCloner
	{
		public static T Clone<T>(T value, ReferenceReuseOptions referenceReuseStrategy)
		{	
			return Clone(value, new ISerialisationTypeConverter[0], new IDeserialisationTypeConverter[0], referenceReuseStrategy);
		}

		public static T Clone<T>(
			T value,
			ISerialisationTypeConverter[] serialisationTypeConverters,
			IDeserialisationTypeConverter[] deserialisationTypeConverters,
			ReferenceReuseOptions referenceReuseStrategy)
		{
			return BinarySerialisation.Deserialise<T>(
				BinarySerialisation.Serialise(value, serialisationTypeConverters, referenceReuseStrategy),
				deserialisationTypeConverters
			);
		}
	}
}