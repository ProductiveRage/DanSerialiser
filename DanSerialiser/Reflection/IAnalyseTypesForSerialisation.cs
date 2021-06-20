using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal interface IAnalyseTypesForSerialisation
	{
		/// <summary>
		/// If unable to resolve the type, this will throw an exception when ignoreAnyInvalidTypes is false; otherwise return null when it's true.
		/// </summary>
		Type GetType(string typeName, bool ignoreAnyInvalidTypes);

		Func<object> TryToGetUninitialisedInstanceBuilder(string typeName);
		Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]> GetFieldsAndProperties(Type type);
		FieldInfo[] GetAllFieldsThatShouldBeSet(Type type);
		MemberAndWriter<FieldInfo> TryToFindField(Type type, string fieldName, string specificTypeNameIfRequired);

		/// <summary>
		/// There may be cases where a value is deserialised for a field that does not exist on the destination type - this could happen if data from an old version of the type is
		/// being deserialised into a new version of the type, in which case we need to check for any [Deprecated(replacedBy: ..)] properties that exist to provide a way to take
		/// this value and set the [Deprecated] property/ies using the field that no longer exists. Note that it's also possible that the serialised data contains a field that
		/// does not exist on the destination type because the serialised data is from a newer version of the type and the destination is an older version of that type that
		/// has never had the field - in that case, this method wil return null.
		/// </summary>
		DeprecatedPropertySettingDetails TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable);
	}
}