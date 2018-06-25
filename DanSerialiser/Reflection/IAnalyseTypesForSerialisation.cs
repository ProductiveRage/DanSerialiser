using System;
using System.Collections.Generic;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal interface IAnalyseTypesForSerialisation
	{
		Func<object> TryToGetUninitialisedInstanceBuilder(string typeName);
		Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>> GetFieldsAndProperties(Type type);
		FieldInfo[] GetAllFieldsThatShouldBeSet(Type type);
		MemberAndWriter<FieldInfo> TryToFindField(Type type, string fieldName, string specificTypeNameIfRequired);
		(Action<object, object>[], FieldInfo[]) GetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable);
	}
}