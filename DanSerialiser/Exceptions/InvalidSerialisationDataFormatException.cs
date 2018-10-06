using System;
using System.Runtime.Serialization;

namespace DanSerialiser.Exceptions
{
	/// <summary>
	/// This will be thrown by the deserialisation process if the data being consumed does not match expectations / requirements - this will relate to a generic
	/// failure caused by invalid data, such as a corrupted content (or trying to deserialise data that was compressed but not decompressed before attempting to
	/// deserialise it) or potentially an attempt to deserialise data in an old version of this library that was serialised by a new version (major versions of
	/// the library may introduce some breaking changes or new serialisation features that the older versions are not aware of; it would be advisable not to
	/// rely upon  new features from later versions until the library can be updated across all projects using it - these changes should be infrequent and
	/// major versions of the library should always produce compatible data and new versions of the library should continue to be able to deserialise data
	/// written by older versions, so if the library is updated then any serialised data persisted externally - such as on disk on in Redis, for example -
	/// should continue to be deserialisable after a library update, even across major versions).
	/// 
	/// Deserialisation failures that relate to more specific circumstances (and ones that you have more direct control over) will be of different types,
	/// such as the FieldNotPresentInSerialisedDataException (which may occur if a new property or field is added to a type and not annotate with attribute
	/// [OptionalWhenDeserialisingAttribute]) or the framework's TypeLoadException (for cases where the serialised data tries to set a field or property by
	/// instantiating a type that is not in a loaded assembly).
	/// </summary>
	[Serializable]
	public sealed class InvalidSerialisationDataFormatException : Exception
	{
		public InvalidSerialisationDataFormatException(string message) : base(message)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentException($"Null/blank {nameof(message)} specified");
		}

		public InvalidSerialisationDataFormatException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}