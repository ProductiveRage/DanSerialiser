using System;
using System.Runtime.Serialization;

namespace DanSerialiser
{
    public sealed class CircularReferenceException : Exception
    {
        public CircularReferenceException() : base() { }
        public CircularReferenceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}