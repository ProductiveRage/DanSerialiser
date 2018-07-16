using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal interface IAnalyseTypesForSerialisation
	{
		Func<object> TryToGetUninitialisedInstanceBuilder(string typeName);
		Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]> GetFieldsAndProperties(Type type);
		FieldInfo[] GetAllFieldsThatShouldBeSet(Type type);
		MemberAndWriter<FieldInfo> TryToFindField(Type type, string fieldName, string specificTypeNameIfRequired);
		(PropertySetter[], FieldInfo[]) GetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable);
	}
}