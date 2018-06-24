using System;
using System.Collections.Generic;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal interface IReadValues
	{
		Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>> GetFieldsAndProperties(Type type);
	}
}