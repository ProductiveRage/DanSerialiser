using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace DanSerialiser.Reflection
{
	internal sealed class ReflectionTypeAnalyser : IAnalyseTypesForSerialisation
	{
		public static ReflectionTypeAnalyser Instance { get; } = new ReflectionTypeAnalyser();
		private ReflectionTypeAnalyser() { }

		public Func<object> TryToGetUninitialisedInstanceBuilder(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			var typeIfAvailable = Type.GetType(typeName, throwOnError: false);
			if (typeIfAvailable == null)
				return null;

			return () => FormatterServices.GetUninitializedObject(typeIfAvailable);
		}

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
				{
					if (property.GetIndexParameters().Length == 0)
						properties.Add(new MemberAndReader<PropertyInfo>(property, GetPropertyReader(property)));
				}
				currentTypeToEnumerateMembersFor = currentTypeToEnumerateMembersFor.BaseType;
			}
			return Tuple.Create<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>(fields, properties);
		}

		public FieldInfo[] GetAllFieldsThatShouldBeSet(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var fields = new List<FieldInfo>();
			var currentType = type;
			while (currentType != null)
			{
				foreach (var field in currentType.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags))
				{
					if (!BinaryReaderWriterShared.IgnoreField(field)
					&& (field.GetCustomAttribute<OptionalWhenDeserialisingAttribute>() == null)
					&& (BackingFieldHelpers.TryToGetPropertyRelatingToBackingField(field)?.GetCustomAttribute<OptionalWhenDeserialisingAttribute>() == null))
						fields.Add(field);
				}
				currentType = currentType.BaseType;
			}
			return fields.ToArray();
		}

		public MemberAndWriter<FieldInfo> TryToFindField(Type type, string fieldName, string specificTypeNameIfRequired)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			while (type != null)
			{
				var field = type.GetField(fieldName, BinaryReaderWriterShared.MemberRetrievalBindingFlags);
				if ((field != null) && ((specificTypeNameIfRequired == null) || (field.DeclaringType.AssemblyQualifiedName == specificTypeNameIfRequired)))
					return new MemberAndWriter<FieldInfo>(field, BinaryReaderWriterShared.IgnoreField(field) ? null : GetFieldWriter(field));
				type = type.BaseType;
			}
			return null;
		}

		public (Action<object, object>[], FieldInfo[]) GetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable)
		{
			if (typeToLookForPropertyOn == null)
				throw new ArgumentNullException(nameof(typeToLookForPropertyOn));
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			var fieldsToConsiderToHaveBeenSet = new List<FieldInfo>();
			var propertySetters = new List<Action<object, object>>();
			var propertyName = BackingFieldHelpers.TryToGetNameOfPropertyRelatingToBackingField(fieldName) ?? fieldName;
			while (typeToLookForPropertyOn != null)
			{
				if ((typeNameIfRequired == null) || (typeToLookForPropertyOn.AssemblyQualifiedName == typeNameIfRequired))
				{
					// TODO: Not sure about this fieldValueTypeIfAvailable business!
					var deprecatedProperty = typeToLookForPropertyOn.GetProperties(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
						.Where(p =>
							(p.Name == propertyName) &&
							(p.DeclaringType == typeToLookForPropertyOn) &&
							(p.GetIndexParameters().Length == 0) &&
							(((fieldValueTypeIfAvailable == null) && !p.PropertyType.IsValueType) || ((fieldValueTypeIfAvailable != null) && p.PropertyType.IsAssignableFrom(fieldValueTypeIfAvailable)))
						)
						.Select(p => new { Property = p, ReplaceBy = p.GetCustomAttribute<DeprecatedAttribute>()?.ReplacedBy })
						.FirstOrDefault(p => p.ReplaceBy != null); // Safe to use FirstOrDefault because there can't be multiple [Deprecated] as AllowMultiple is not set to true on the attribute class
					if (deprecatedProperty != null)
					{
						// Try to find a field that the "ReplacedBy" value relates to (if we find it then we'll consider it to have been set because setting the
						// deprecated property should set it))
						propertySetters.Add(GetPropertyWriter(deprecatedProperty.Property));
						var field = typeToLookForPropertyOn.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
							.Where(f => (f.Name == deprecatedProperty.ReplaceBy) && (f.DeclaringType == typeToLookForPropertyOn))
							.FirstOrDefault();
						if (field == null)
						{
							// If the "ReplacedBy" value didn't directly match a field then try to find a property that it matches and then see if there is a
							// backing field for that property that we can set (if we find this then we'll consider to have been set because setting the deprecated
							// property should set it)
							var property = typeToLookForPropertyOn.GetProperties(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
								.Where(p => (p.Name == deprecatedProperty.ReplaceBy) && (p.DeclaringType == typeToLookForPropertyOn) && (p.GetIndexParameters().Length == 0))
								.FirstOrDefault();
							if (property != null)
							{
								var nameOfPotentialBackingFieldForProperty = BackingFieldHelpers.GetBackingFieldName(property.Name);
								field = typeToLookForPropertyOn.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
									.Where(f => (f.Name == nameOfPotentialBackingFieldForProperty) && (f.DeclaringType == typeToLookForPropertyOn))
									.FirstOrDefault();
							}
						}
						if (field != null)
						{
							// Although the field hasn't directly been set, it should have been set indirectly by setting the property value above (unless the [Deprecated]
							// "ReplaceBy" value was lying)
							fieldsToConsiderToHaveBeenSet.Add(field);
						}
					}
				}
				typeToLookForPropertyOn = typeToLookForPropertyOn.BaseType;
			}
			return (propertySetters.ToArray(), fieldsToConsiderToHaveBeenSet.ToArray());
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

		private static Action<object, object> GetFieldWriter(FieldInfo field)
		{
			if (field.DeclaringType.IsValueType)
				return (source, value) => field.SetValue(source, value); // TODO: The below needs some tweaking to work with structs

			// Can't set readonly fields using LINQ Expressions, need  to resort to emitting IL
			var method = new DynamicMethod(
				name: "Set" + field.Name,
				returnType: null,
				parameterTypes: new[] { typeof(object), typeof(object) },
				m: field.DeclaringType.Module,
				skipVisibility: true
			);
			var gen = method.GetILGenerator();
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Castclass, field.DeclaringType);
			gen.Emit(OpCodes.Ldarg_1);
			if (field.FieldType.IsValueType)
				gen.Emit(OpCodes.Unbox_Any, field.FieldType);
			else
				gen.Emit(OpCodes.Castclass, field.FieldType);
			gen.Emit(OpCodes.Stfld, field);
			gen.Emit(OpCodes.Ret);
			return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
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

		private static Action<object, object> GetPropertyWriter(PropertyInfo property)
		{
			var sourceParameter = Expression.Parameter(typeof(object), "source");
			var valueParameter = Expression.Parameter(typeof(object), "value");
			return
				Expression.Lambda<Action<object, object>>(
					Expression.Assign(
						Expression.MakeMemberAccess(
							Expression.Convert(sourceParameter, property.DeclaringType),
							property
						),
						Expression.Convert(valueParameter, property.PropertyType)
					),
					sourceParameter,
					valueParameter
				)
				.Compile();
		}
	}
}