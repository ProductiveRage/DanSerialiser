using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static T Clone<T>(T value)
		{
			var writer = new BinaryWriter();
			Serialiser.Instance.Serialise(value, writer);
			var reader = new BinaryReader(writer.GetData());
			return reader.Read<T>();
		}
	}
}