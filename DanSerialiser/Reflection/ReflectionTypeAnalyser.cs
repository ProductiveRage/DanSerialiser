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
		private static readonly MethodInfo _getDefaultMethod = typeof(ReflectionTypeAnalyser).GetMethod(nameof(GetDefault), BindingFlags.NonPublic | BindingFlags.Static);

		public static ReflectionTypeAnalyser Instance { get; } = new ReflectionTypeAnalyser();
		private ReflectionTypeAnalyser() { }

		/// <summary>
		/// This will throw an exception if unable to resolve the type (it will never return null)
		/// </summary>
		public Type GetType(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			// Some real world serialisation testing showed that sometimes calling Type.GetType inside ReadNextArray in the BinarySerialisationReader was taking a lot of
			// the processing time (it was particularly noticeable one time and less so others.. I'm not sure why) and so IAnalyseTypesForSerialisation has a GetType method
			// so that the caching implementation of it can avoid the repeated Type.GetType calls (http://higherlogics.blogspot.com/2010/05/cost-of-typegettype.html suggests
			// that I'm not the only one to have noticed this!)
			return Type.GetType(typeName, throwOnError: true);
		}

		public Func<object> TryToGetUninitialisedInstanceBuilder(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			var typeIfAvailable = Type.GetType(typeName, throwOnError: false);
			if (typeIfAvailable == null)
				return null;

			// If it's a struct then we can avoid anything complicated like searching for a constructor or calling GetUninitializedObject because structs are designed to
			// be easily instantiated in an uninitialised state (since they can never be null) - we just call default(T)
			if (typeIfAvailable.IsValueType)
			{
				return Expression.Lambda<Func<object>>(
					Expression.Convert(
						Expression.Call(_getDefaultMethod.MakeGenericMethod(typeIfAvailable)),
						typeof(object)
					)
				)
				.Compile();
			}

			// https://rogerjohansson.blog/2016/08/16/wire-writing-one-of-the-fastest-net-serializers/ suggested that calling a parameterless constructor will be faster
			// than GetUninitializedObject. I've found it to be MARGINALLY faster but it's an interesting enough optimisation to leave in for now!
			var parameterLessConstructor = typeIfAvailable.GetConstructor(BinaryReaderWriterShared.MemberRetrievalBindingFlags, null, Type.EmptyTypes, null);
			if (parameterLessConstructor != null)
			{
				// Try to guess whether this is an empty constructor or not (we don't want to call one that wil cause side effects - it could even throw an exception,
				// potentially). This trick is also from that URL above. This guesswork does feel a touch hairy and the performance improvement is not as large as I'd
				// hoped, so it may get binned off one day if it starts causing any trouble.
				if (parameterLessConstructor.GetMethodBody().GetILAsByteArray().Length <= 8)
					return Expression.Lambda<Func<object>>(Expression.New(parameterLessConstructor)).Compile();
			}

			return () => FormatterServices.GetUninitializedObject(typeIfAvailable);
		}

		private static T GetDefault<T>() => default(T);

		public Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]> GetFieldsAndProperties(Type type)
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
			return Tuple.Create(fields.ToArray(), properties.ToArray());
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
				// Note: If there is a type name specified then it will be a Type's "FullName" value and not its "AssemblyQualifiedName" because the AssemblyQualifiedName includes
				// the version number of the assembly and if there is a type name here then the chances are that this is to do with a [Deprecated] property, which means that the
				// versions of the assembly are expected to be different between now and when the serialised data was generated for some deserialisation attempts (when type
				// names appear elsewhere - for types to be instantiated - the AssemblyQualifiedName is required, though the Type.GetType method used to create instances
				// is conveniently flexible with version numbers - eg.
				//
				//   Type.GetType("Tester.PersonDetails, Tester, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null")
				//
				// will work even if the currently assembly version of Tester is a version other than 4.0.0.0 (TODO: So long as the type's assembly has already been loaded into
				// memory by that time - which is often the case, I hope, when the project has been built to reference the entities that will be de/serialised but which is possibly
				// something that could be improved.. it's something that I'm finding awkward to unit test at the moment, which I'm not happy about)
				var field = type.GetField(fieldName, BinaryReaderWriterShared.MemberRetrievalBindingFlags);
				if ((field != null) && ((specificTypeNameIfRequired == null) || (field.DeclaringType.FullName == specificTypeNameIfRequired)))
					return new MemberAndWriter<FieldInfo>(field, BinaryReaderWriterShared.IgnoreField(field) ? null : GetFieldWriter(field));
				type = type.BaseType;
			}
			return null;
		}

		/// <summary>
		/// There may be cases where a value is deserialised for a field that does not exist on the destination type - this could happen if data from an old version of the type is
		/// being deserialised into a new version of the type, in which case we need to check for any [Deprecated(replacedBy: ..)] properties that exist to provide a way to take
		/// this value and set the [Deprecated] property/ies using the field that no longer exists. Note that it's also possible that the serialised data contains a field that
		/// does not exist on the destination type because the serialised data is from a newer version of the type and the destination is an older version of that type that
		/// has never had the field - in that case, this method wil return null.
		/// </summary>
		public DeprecatedPropertySettingDetails TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable)
		{
			if (typeToLookForPropertyOn == null)
				throw new ArgumentNullException(nameof(typeToLookForPropertyOn));
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			var propertySetters = new List<Tuple<PropertyInfo, MemberUpdater>>();
			var fieldsToConsiderToHaveBeenSetViaDeprecatedProperties = new List<FieldInfo>();
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
						propertySetters.Add(Tuple.Create(deprecatedProperty.Property, GetPropertyWriter(deprecatedProperty.Property)));
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
							fieldsToConsiderToHaveBeenSetViaDeprecatedProperties.Add(field);
						}
					}
				}
				typeToLookForPropertyOn = typeToLookForPropertyOn.BaseType;
			}
			if (!propertySetters.Any())
				return null;

			Type compatibleCommonPropertyTypeIfKnown = null;
			foreach (var propertySetter in propertySetters)
			{
				if (compatibleCommonPropertyTypeIfKnown == null)
					compatibleCommonPropertyTypeIfKnown = propertySetter.Item1.PropertyType;
				else if (propertySetter.Item1.PropertyType != compatibleCommonPropertyTypeIfKnown)
				{
					if (compatibleCommonPropertyTypeIfKnown.IsAssignableFrom(propertySetter.Item1.PropertyType))
					{
						// If this property is of a more specific type than propertyTypeIfKnown but the current propertyTypeIfKnown could be satisfied by it then record
						// this type going forward (any other properties will need to either be this type of be assignable to it otherwise we won't be able to deserialise
						// a single value that can be used to populate all of the related properties - only for cases where there are multiple, obviously)
						compatibleCommonPropertyTypeIfKnown = propertySetter.Item1.PropertyType;
					}
					else if (!propertySetter.Item1.PropertyType.IsAssignableFrom(compatibleCommonPropertyTypeIfKnown))
					{
						// If propertySetter.PropertyType is not the same as propertyTypeIfKnown and if propertyTypeIfKnown is not assignable from propertySetter.PropertyType
						// and propertySetter.PropertyType is not assigned from propertyTypeIfKnown then we've come to an impossible situation - if there are multiple properties
						// then there must be a single type that a value may be deserialised as that may be used to set ALL of the properties. Since there isn't, we have to throw.
						throw new InvalidOperationException($"Type {typeToLookForPropertyOn.Name} has [Deprecated] properties that are all set by data for field {fieldName} but which have incompatible types");
					}
				}
			}
			return new DeprecatedPropertySettingDetails(
				compatibleCommonPropertyTypeIfKnown, 
				propertySetters.Select(p => p.Item2).ToArray(),
				fieldsToConsiderToHaveBeenSetViaDeprecatedProperties.ToArray()
			);
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

		private static MemberUpdater GetFieldWriter(FieldInfo field)
		{
			return field.DeclaringType.IsValueType
				? GetFieldWriterForValueType(field)
				: GetFieldWriterForReferenceType(field);
		}

		private static MemberUpdater GetFieldWriterForValueType(FieldInfo field)
		{
			var dynamicMethod = new DynamicMethod(
				name: "Set" + field.Name,
				returnType: null,
				parameterTypes: new[] { typeof(object).MakeByRefType(), typeof(object) },
				m: field.DeclaringType.Module,
				skipVisibility: true
			);

			var gen = dynamicMethod.GetILGenerator();

			// var typedSource = (T)source;
			var typedSource = gen.DeclareLocal(field.DeclaringType);
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldind_Ref);
			gen.Emit(OpCodes.Unbox_Any, field.DeclaringType);
			gen.Emit(OpCodes.Stloc_0);

			// typedSource.Id = (TField)id;
			gen.Emit(OpCodes.Ldloca_S, typedSource);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Unbox_Any, field.FieldType);
			gen.Emit(OpCodes.Stfld, field);

			// source = typedSource;
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldloc_0);
			gen.Emit(OpCodes.Box, field.DeclaringType);
			gen.Emit(OpCodes.Stind_Ref);
			gen.Emit(OpCodes.Ret);

			return (MemberUpdater)dynamicMethod.CreateDelegate(typeof(MemberUpdater));
		}

		private static MemberUpdater GetFieldWriterForReferenceType(FieldInfo field)
		{
			// Can't set readonly fields using LINQ Expressions, need  to resort to emitting IL
			var method = new DynamicMethod(
				name: "Set" + field.Name,
				returnType: null,
				parameterTypes: new[] { typeof(object).MakeByRefType(), typeof(object) },
				m: field.DeclaringType.Module,
				skipVisibility: true
			);
			var gen = method.GetILGenerator();
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldind_Ref);
			gen.Emit(OpCodes.Castclass, field.DeclaringType);
			gen.Emit(OpCodes.Ldarg_1);
			if (field.FieldType.IsValueType)
				gen.Emit(OpCodes.Unbox_Any, field.FieldType);
			else
				gen.Emit(OpCodes.Castclass, field.FieldType);
			gen.Emit(OpCodes.Stfld, field);
			gen.Emit(OpCodes.Ret);
			return (MemberUpdater)method.CreateDelegate(typeof(MemberUpdater));
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

		private static MemberUpdater GetPropertyWriter(PropertyInfo property)
		{
			var sourceParameter = Expression.Parameter(typeof(object).MakeByRefType(), "source");
			var valueParameter = Expression.Parameter(typeof(object), "value");
			return Expression.Lambda<MemberUpdater>(
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