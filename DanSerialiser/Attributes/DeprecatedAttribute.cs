using System;

namespace DanSerialiser
{
	/// <summary>
	/// This attribute should be used in scenarios where a property on a type is being replaced but it is desirable that when the new type is serialised, that data may be deserialised
	/// into the old version of the type. For example, if V1 of a class has a string property 'Name' that is to be replaced on the V2 class with a property called 'TranslatedName' that
	/// is of type TranslatedString then a computed 'Name' property may be maintained on the V2 type which returns the default language content of the TranslatedString value - this
	/// would mean that any code that was compiled against the V1 type would continue to work when compiled against the V2 type. However, if code compiled against the V1 type tries to
	/// deserialise an instance of this type where the serialisation occurred where the V2 type was loaded then it would fail as the assembly referencing the V1 type is not aware of
	/// the new 'TranslatedName' property and the computed 'Name' property would not contribute to the serialised data.. unless this attribute is added to the computed property. When
	/// this attribute is added to a property, its value will be included in the serialised data and so deserialisation to V1 of the type will succeed.
	/// 
	/// The attribute may also be used to deserialise data into a new version of a type. If the computed property has a setter that sets the new property and if the Deprecated attribute
	/// has a 'ReplacedBy' value that corresponds to the name of the new property then the deserialisation will succeed. To continue the example above, the 'Name' property setter would
	/// create a new TranslatedString value for the new 'TranslatedName' property. If the type is otherwise designed to be immutable then the computed property setter may be made private
	/// (if the computed property is only required for the serialisation process and not to maintain compatibility with code that may be using the latest version of the type but still
	/// referencing the old property) then the property itself may be made private.
	/// 
	/// If types are only ever changed by way of adding new properties or by replacing properties but adding computed 'compatibility' properties (that have the Deprecated attribute on
	/// them) then serialised data will be forwards and backwards compatible - older versions of a type may be initialised from serialised data generated from newer versions of a type
	/// and vice versa.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class DeprecatedAttribute : Attribute
	{
		public DeprecatedAttribute(string replacedBy = null)
		{
			ReplacedBy = string.IsNullOrWhiteSpace(replacedBy) ? null : replacedBy;
		}

		/// <summary>
		/// This value is required if serialised data must be 'forwards compatible', such that it must be possible for serialised data generated from older versions of a type to be
		/// used to deserialise to a new version of that type. In that case, the deprecated property should have a setter that sets the property that exists on the newer version and
		/// this ReplacedBy value should be the name of that new property.
		/// 
		/// If serialised data only needs to be 'backwards compatible', such that it must be possible for serialised data generated from a newer version of type to be used to initialise
		/// an older version of a type, then the 'ReplacedBy' value is not required and may be null.
		/// </summary>
		public string ReplacedBy { get; }
	}
}