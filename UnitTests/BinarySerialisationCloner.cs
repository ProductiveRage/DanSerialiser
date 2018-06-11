using System;
using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static byte[] Serialise(object value)
		{
			var writer = new BinaryWriter();
			Serialiser.Instance.Serialise(value, writer);
			return writer.GetData();
		}

		public static T Deserialise<T>(byte[] serialisedData)
		{
			if (serialisedData == null)
				throw new ArgumentNullException(nameof(serialisedData));

			var reader = new BinaryReader(serialisedData);
			return reader.Read<T>();
		}

		public static T Clone<T>(T value)
		{
			return Deserialise<T>(Serialise(value));
		}
	}
}