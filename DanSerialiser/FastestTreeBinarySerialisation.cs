using System;
using System.IO;

namespace DanSerialiser
{
	public static class FastestTreeBinarySerialisation
	{
		/// <summary>
		/// This uses the BinarySerialisationWriter (and a MemoryStream) in a configuration that disables reference reuse and circular reference tracking, which enables additional
		/// optimisations to be made to the process for faster serialisation. Not only is it limited in terms of reference tracking but it also does not allow for any serialisation
		/// type converters to be specified. This serialisation method should not be used with data in which the same references appear multiple times - if there are any circular
		/// references then a stack overflow exception will be thrown and if the same references are repeated many times within the data then the standard Binary Serialisation
		/// Writer may provide the best performance - but it may offer the best serialisation performance if your data structure meets these requirements. It still has support
		/// for the DanSerialiser attributes that affect entity versioning (such as Deprecated and OptionalWhenDeserialising) and its output may still be consumed by the
		/// BinarySerialisationReader. Note: Using sealed classes wherever possible allows this serialisation process to enable more optimisations in some cases
		/// and so that is a recommended practice if you want the fastest results.
		/// </summary>
		public static byte[] Serialise(object value)
		{
			using (var stream = new MemoryStream())
			{
				var writer = GetSerialisationWriter(stream);
				Serialiser.Instance.Serialise(value, new ISerialisationTypeConverter[0], writer);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// This may be used if you wish to perform the serialisation on a particular stream, rather than having to generate a byte array. The same notes apply as to the 'Serialise'
		/// method in this class - this should offer the most efficient binary serialisation that this library has to offer, so long as your data meets the requirements outlined in
		/// that method's summary comment.
		/// </summary>
		public static IWrite GetSerialisationWriter(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			return new BinarySerialisationWriter(stream, ReferenceReuseOptions.SpeedyButLimited);
		}
	}
}