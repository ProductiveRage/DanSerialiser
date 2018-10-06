using System;
using System.Runtime.Serialization;

namespace DanSerialiser
{
	[Serializable]
	public sealed class CircularReferenceException : Exception
	{
		private const string MESSAGE = "Can not serialise data that contains circular references (unless the properties that are involved in the circular references are marked to be ignored)";
		public CircularReferenceException() : base(MESSAGE) { }
		public CircularReferenceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}