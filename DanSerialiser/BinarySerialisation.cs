using System;
using System.IO;

namespace DanSerialiser
{
	public static class BinarySerialisation
	{
		/// <summary>
		/// This uses the BinarySerialisationWriter and a MemoryStream to produce a byte array
		/// </summary>
		public static byte[] Serialise(object value, bool optimiseForWideCircularReference = false)
		{
			return Serialise(
				value,
				optimiseForWideCircularReference
					? ReferenceReuseOptions.OptimiseForWideCircularReferences
					: ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure
			);
		}

		internal static byte[] Serialise(object value, ReferenceReuseOptions referenceReuseStrategy) // internal for unit testing
		{
			using (var stream = new MemoryStream())
			{
				var writer = new BinarySerialisationWriter(stream, referenceReuseStrategy);
				Serialiser.Instance.Serialise(value, writer);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// This uses the BinarySerialisationReader and a MemoryStream to deserialise from byte array
		/// </summary>
		public static T Deserialise<T>(byte[] serialisedData)
		{
			if (serialisedData == null)
				throw new ArgumentNullException(nameof(serialisedData));

			using (var stream = new MemoryStream(serialisedData))
			{
				return (new BinarySerialisationReader(stream)).Read<T>();
			}
		}
	}
}