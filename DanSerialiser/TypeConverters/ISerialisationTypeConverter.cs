namespace DanSerialiser.TypeConverters
{
	/// <summary>
	/// During the serialisation process, it may be desirable to transform some values before serialising them - for example, large linked lists could result in
	/// a stack overflow exception as the serialiser steps through each item in the chain and so it may be better if it were replaced with an array. In order for
	/// the resulting data to be deserialised, an IDeserialisationTypeConverter will be required that can translate the array back into the linked list type.
	/// </summary>
	public interface ISerialisationTypeConverter
	{
		/// <summary>
		/// This should return the same value if no transformation is required. Both the value argument and the return value are allowed to be null. If there
		/// are multiple type converters specified for the serialiser then the first one that changes the value will be used and any others will be ignored.
		/// </summary>
		object ConvertIfRequired(object value);
	}
}