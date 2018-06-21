using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static T Clone<T>(T value, bool supportReferenceReuse)
		{
			return BinarySerialisation.Deserialise<T>(BinarySerialisation.Serialise(value, supportReferenceReuse));
		}
	}
}