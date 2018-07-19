using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	/// <summary>
	/// If a field in the serialised data does not exist on the destination type then there is a chance that it is because the serialised data comes from an older version of a type and the
	/// new version replaced that field with something different - in order for the serialised data to be compatible, there must be a [Deprecated(replacedBy: ..)] property on the new version
	/// of the type with a setter that may be called with the value for the field that no longer exists. This class describes information about that scenario - it's feasible that there could
	/// be multiple properties that are marked as being replacements for the same no-longer-available field (if so, then all of the fields must be of compatible types; if they are not the
	/// same type then then must all be assignable from the most specific type that any of them has - this will be the CompatibleTypeToReadAs property of this class).
	/// </summary>
	internal sealed class DeprecatedPropertySettingDetails
	{
		public DeprecatedPropertySettingDetails(Type compatibleTypeToReadAs, MemberUpdater[] propertySetters, FieldInfo[] relatedFieldsThatHaveBeenSetViaTheDeprecatedProperties)
		{
			CompatibleTypeToReadAs = compatibleTypeToReadAs ?? throw new ArgumentNullException(nameof(compatibleTypeToReadAs));
			PropertySetters = propertySetters ?? throw new ArgumentNullException(nameof(propertySetters));
			RelatedFieldsThatHaveBeenSetViaTheDeprecatedProperties = relatedFieldsThatHaveBeenSetViaTheDeprecatedProperties ?? throw new ArgumentNullException(nameof(relatedFieldsThatHaveBeenSetViaTheDeprecatedProperties));
		}

		public Type CompatibleTypeToReadAs { get; }
		public MemberUpdater[] PropertySetters { get; }
		public FieldInfo[] RelatedFieldsThatHaveBeenSetViaTheDeprecatedProperties { get; }
	}
}