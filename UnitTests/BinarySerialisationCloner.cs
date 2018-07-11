using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static T Clone<T>(T value, ReferenceReuseOptions referenceReuseStrategy)
		{
			return BinarySerialisation.Deserialise<T>(BinarySerialisation.Serialise(value, referenceReuseStrategy));
		}
	}
}