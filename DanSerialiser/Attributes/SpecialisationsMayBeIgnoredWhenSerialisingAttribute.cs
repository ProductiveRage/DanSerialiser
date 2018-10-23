using System;

namespace DanSerialiser
{
	/// <summary>
	/// This may be applied to a field or property on a type that will be serialised to indicate that references assigned to that field or property will always match the field / property
	/// type precisely and never be a more specialised type (for example, if the property type is MyList and if MyList is a non-sealed class then this attribute may be added to that
	/// property to tell the serialisation process that the value will ALWAYS be an instance of MyList or null - and not an instance of another type that inherits from MyList). It also
	/// has the effect of saying that any fields or properties on the specified type that are interfaces or abstract types are optional and that it is acceptable for the serialiser to
	/// write away null values for them. It should be noted, though, that this attribute is not guaranteed to affect serialisation behaviour but that it opens the door for serialisers
	/// to perform more aggressive optimisations if the serialiser is configured to do so (currently, only the FastestTreeBinarySerialisation methods will take advantage of this attribute).
	/// 
	/// This attribute should only be applied to fields or properties whose types are non-sealed, non-abstract classes - it does not make sense for primitives or value types or sealed
	/// classes (because they have no opportunity to be derived into specialised types) and it does not makes sense for abstract classes or interfaces (because they require derived
	/// classes / implementations in order to have meaning).
	/// 
	/// You must be careful when considering using this attribute on a field or property because it applies to the type of that field or property AND to any nested type - this means that
	/// it would apply to the key and value types of a Dictionary, for example, so you must be certain that that will not cause any problems. This is not intended for general purpose
	/// serialisation, it should only be used when you are sure that you want to accept the compromises in exchange for a little more serialisation performance (and it will only help
	/// that in cases where reference reuse tracking is enabled - which, again, is not recommended for general purpose serialisation since any circular references will result in a
	/// stack overflow exception).
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	internal // 2018-10-23: I've made this internal instead of public (and may delete it entirely in the future) as I'm not happy with how easy it is for edge cases to slip in when it's used
		sealed class SpecialisationsMayBeIgnoredWhenSerialisingAttribute : Attribute { }
}