using System;
using System.IO;
using DanSerialiser.TypeConverters;

namespace DanSerialiser
{
	public static class BinarySerialisation
	{
		/// <summary>
		/// This uses the BinarySerialisationWriter and a MemoryStream to produce a byte array. The default approach to the serialisation process is to presume that the value is
		/// a tree-like structure and to traverse each branch to its end - if the object model has large arrays whose elements are the starts of circular reference chains then
		/// this can cause a stack overflow exception. If the value that you want to serialise sounds like that then setting optimiseForWideCircularReference to true will change
		/// how the serialiser approaches the data and should fix the problem - this alternate approach is more expensive, though (both to serialise and deserialise), and so it
		/// is recommended that you only enable it if you have to. Note that the Deserialise method does not take this argument, the deserialisation process will be able to tell
		/// from the incoming binary data whether the serialiser had optimiseForWideCircularReference enabled or not.
		/// </summary>
		public static byte[] Serialise(object value, bool optimiseForWideCircularReference = false) => Serialise(value, new ISerialisationTypeConverter[0], optimiseForWideCircularReference);

		/// <summary>
		/// This uses the BinarySerialisationWriter and a MemoryStream to produce a byte array. The default approach to the serialisation process is to presume that the value is
		/// a tree-like structure and to traverse each branch to its end - if the object model has large arrays whose elements are the starts of circular reference chains then
		/// this can cause a stack overflow exception. If the value that you want to serialise sounds like that then setting optimiseForWideCircularReference to true will change
		/// how the serialiser approaches the data and should fix the problem - this alternate approach is more expensive, though (both to serialise and deserialise), and so it
		/// is recommended that you only enable it if you have to. Note that the Deserialise method does not take this argument, the deserialisation process will be able to tell
		/// from the incoming binary data whether the serialiser had optimiseForWideCircularReference enabled or not.
		/// </summary>
		public static byte[] Serialise(object value, ISerialisationTypeConverter[] typeConverters, bool optimiseForWideCircularReference = false)
		{
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));

			return Serialise(
				value,
				typeConverters,
				optimiseForWideCircularReference
					? ReferenceReuseOptions.OptimiseForWideCircularReferences
					: ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure
			);
		}

		internal static byte[] Serialise(object value, ISerialisationTypeConverter[] typeConverters, ReferenceReuseOptions referenceReuseStrategy) // internal for unit testing
		{
			using (var stream = new MemoryStream())
			{
				var writer = new BinarySerialisationWriter(stream, referenceReuseStrategy);
				Serialiser.Instance.Serialise(value, typeConverters, writer);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// This uses the BinarySerialisationReader and a MemoryStream to deserialise from byte array
		/// </summary>
		public static T Deserialise<T>(byte[] serialisedData) => Deserialise<T>(serialisedData, new IDeserialisationTypeConverter[0]);

		/// <summary>
		/// This uses the BinarySerialisationReader and a MemoryStream to deserialise from byte array
		/// </summary>
		public static T Deserialise<T>(byte[] serialisedData, IDeserialisationTypeConverter[] typeConverters)
		{
			if (serialisedData == null)
				throw new ArgumentNullException(nameof(serialisedData));
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));

			using (var stream = new MemoryStream(serialisedData))
			{
				return (new BinarySerialisationReader(stream, typeConverters)).Read<T>();
			}
		}
	}
}