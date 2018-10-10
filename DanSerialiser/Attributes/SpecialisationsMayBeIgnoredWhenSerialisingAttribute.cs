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
	/// It might be applied, for example, to a property of type Dictionary&lt;int, string&gt; if it is known that the property will only ever be assigned a value of that type (and not a
	/// more specialised type that is derived from it) AND if values will never have a custom equality comparer specified.
	/// 
	/// This attribute should only be applied to fields or properties whose types are non-sealed, non-abstract classes - it does not make sense for primitives or value types or sealed
	/// classes (because they have no opportunity to be derived into specialised types) and it does not makes sense for abstract classes or interfaces (because they require derived
	/// classes / implementations in order to have meaning).
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public sealed class SpecialisationsMayBeIgnoredWhenSerialisingAttribute : Attribute { }
}