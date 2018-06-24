using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class ReflectionReader : IReadValues
	{
		public static ReflectionReader Instance { get; } = new ReflectionReader();
		private ReflectionReader() { }

		public Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>> GetFieldsAndProperties(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var fields = new List<MemberAndReader<FieldInfo>>();
			var properties = new List<MemberAndReader<PropertyInfo>>();
			var currentTypeToEnumerateMembersFor = type;
			while (currentTypeToEnumerateMembersFor != null)
			{
				foreach (var field in currentTypeToEnumerateMembersFor.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags))
					fields.Add(new MemberAndReader<FieldInfo>(field, GetFieldReader(field)));
				foreach (var property in currentTypeToEnumerateMembersFor.GetProperties(BinaryReaderWriterShared.MemberRetrievalBindingFlags))
					properties.Add(new MemberAndReader<PropertyInfo>(property, GetPropertyReader(property)));
				currentTypeToEnumerateMembersFor = currentTypeToEnumerateMembersFor.BaseType;
			}
			return Tuple.Create<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>(fields, properties);
		}

		private static Func<object, object> GetFieldReader(FieldInfo field)
		{
			var sourceParameter = Expression.Parameter(typeof(object), "source");
			return
				Expression.Lambda<Func<object, object>>(
					Expression.Convert(
						Expression.Field(
							Expression.Convert(sourceParameter, field.DeclaringType),
							field
						),
						typeof(object)
					),
					sourceParameter
				)
				.Compile();
		}

		private static Func<object, object> GetPropertyReader(PropertyInfo property)
		{
			var sourceParameter = Expression.Parameter(typeof(object), "source");
			return
				Expression.Lambda<Func<object, object>>(
					Expression.Convert(
						Expression.Property(
							Expression.Convert(sourceParameter, property.DeclaringType),
							property
						),
						typeof(object)
					),
					sourceParameter
				)
				.Compile();
		}
	}
}