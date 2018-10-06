using System;

namespace DanSerialiser.Attributes
{
	/// <summary>
	/// Ordinarily, when deserialising, if there are any members that can not have a value set then the deserialisation will be considered a failure and a FieldNotPresentInSerialisedDataException
	/// will be raised. If particular fields or properties should be considered optional and for it not to be an error case if the deserialisation process can not populate them then this attribute
	/// should be applied to them (this does not apply to fields that have the [NonSerialized] attribute since these are never considered for deserialisation).
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public sealed class OptionalWhenDeserialisingAttribute : Attribute { }
}