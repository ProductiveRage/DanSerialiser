using System;
using System.Runtime.Serialization;

namespace DanSerialiser.Exceptions
{
	[Serializable]
	public sealed class MaxObjectGraphSizeExceededException : Exception
	{
		private static readonly string MESSAGE = $"Can not serialise data with more than {BinaryReaderWriterShared.MaxReferenceCount} non-string object references";
		public MaxObjectGraphSizeExceededException() : base(MESSAGE) { }
		public MaxObjectGraphSizeExceededException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}