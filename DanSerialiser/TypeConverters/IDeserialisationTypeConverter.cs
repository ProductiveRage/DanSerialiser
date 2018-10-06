using System;

namespace DanSerialiser
{
	/// <summary>
	/// If any values were transformed by ISerialisationTypeConverter implementations during the serialisation process then it is almost certain that implementations
	/// of this interface will be required to map the serialised data back onto the original object model - for example, if linked lists are changed into arrays when
	/// serialised then those arrays will need to be changed back into the linked list type before the target field or property can be set (it will be possible to
	/// determine whether an array needs changing into a linked list or whether it may be left as an array as the 'ConvertIfRequired' method receives a
	/// targetType argument as well as the value reference)
	/// </summary>
	public interface IDeserialisationTypeConverter
	{
		/// <summary>
		/// This should return the same value if no transformation is required. Both the value argument and the return value are allowed to be null but this will
		/// not be called with a null targetType reference. If there are multiple type converters specified then the first one that changes the value will be used
		/// and any others will be ignored.
		/// </summary>
		object ConvertIfRequired(Type targetType, object value);
	}
}