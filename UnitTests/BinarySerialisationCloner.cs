using System;
using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static byte[] Serialise(object value, bool supportReferenceReuse)
		{
			var writer = new BinarySerialisationWriter(supportReferenceReuse);
			Serialiser.Instance.Serialise(value, writer);
			return writer.GetData();
		}

		public static T Deserialise<T>(byte[] serialisedData)
		{
			if (serialisedData == null)
				throw new ArgumentNullException(nameof(serialisedData));

			var reader = new BinarySerialisationReader(serialisedData);
			return reader.Read<T>();
		}

		public static T Clone<T>(T value, bool supportReferenceReuse)
		{
			return Deserialise<T>(Serialise(value, supportReferenceReuse));
		}
	}
}