using System;
using System.IO;
using DanSerialiser;

namespace UnitTests
{
	public static class BinarySerialisationCloner
	{
		public static byte[] Serialise(object value, bool supportReferenceReuse)
		{
			using (var stream = new MemoryStream())
			{
				var writer = new BinarySerialisationWriter(stream, supportReferenceReuse);
				Serialiser.Instance.Serialise(value, writer);
				return stream.ToArray();
			}
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