using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class CachingTypeAnalyser : IAnalyseTypesForSerialisation
	{
		private readonly IAnalyseTypesForSerialisation _reader;
		private readonly ConcurrentDictionary<string, Func<object>> _typeBuilderCache;
		private readonly ConcurrentDictionary<Type, Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]>> _fieldAndPropertyCache;
		private readonly ConcurrentDictionary<Type, FieldInfo[]> _requiredFieldCache;
		private readonly ConcurrentDictionary<Tuple<Type, string, string>, MemberAndWriter<FieldInfo>> _fieldNameCache;
		private readonly ConcurrentDictionary<Tuple<Type, string, string, Type>, DeprecatedPropertySettingDetails> _deprecatedPropertyCache;
		public CachingTypeAnalyser(IAnalyseTypesForSerialisation reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
			_typeBuilderCache = new ConcurrentDictionary<string, Func<object>>();
			_fieldAndPropertyCache = new ConcurrentDictionary<Type, Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]>>();
			_requiredFieldCache = new ConcurrentDictionary<Type, FieldInfo[]>();
			_fieldNameCache = new ConcurrentDictionary<Tuple<Type, string, string>, MemberAndWriter<FieldInfo>>();
			_deprecatedPropertyCache = new ConcurrentDictionary<Tuple<Type, string, string, Type>, DeprecatedPropertySettingDetails>();
		}

		public Func<object> TryToGetUninitialisedInstanceBuilder(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			if (_typeBuilderCache.TryGetValue(typeName, out var cachedResult))
				return cachedResult;

			var result = _reader.TryToGetUninitialisedInstanceBuilder(typeName);
			_typeBuilderCache.TryAdd(typeName, result);
			return result;
		}

		public Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]> GetFieldsAndProperties(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_fieldAndPropertyCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			var result = _reader.GetFieldsAndProperties(type);
			_fieldAndPropertyCache.TryAdd(type, result);
			return result;
		}

		public FieldInfo[] GetAllFieldsThatShouldBeSet(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_requiredFieldCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			var result = _reader.GetAllFieldsThatShouldBeSet(type);
			_requiredFieldCache.TryAdd(type, result);
			return result;
		}

		public MemberAndWriter<FieldInfo> TryToFindField(Type type, string fieldName, string specificTypeNameIfRequired)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			var cacheKey = Tuple.Create(type, fieldName, specificTypeNameIfRequired);
			if (_fieldNameCache.TryGetValue(cacheKey, out var cachedResult))
				return cachedResult;

			var result = _reader.TryToFindField(type, fieldName, specificTypeNameIfRequired);
			_fieldNameCache.TryAdd(cacheKey, result);
			return result;
		}

		public DeprecatedPropertySettingDetails TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(Type typeToLookForPropertyOn, string fieldName, string typeNameIfRequired, Type fieldValueTypeIfAvailable)
		{
			if (typeToLookForPropertyOn == null)
				throw new ArgumentNullException(nameof(typeToLookForPropertyOn));
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			var cacheKey = Tuple.Create(typeToLookForPropertyOn, fieldName, typeNameIfRequired, fieldValueTypeIfAvailable);
			if (_deprecatedPropertyCache.TryGetValue(cacheKey, out var cachedResult))
				return cachedResult;

			var result = _reader.TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(typeToLookForPropertyOn, fieldName, typeNameIfRequired, fieldValueTypeIfAvailable);
			_deprecatedPropertyCache.TryAdd(cacheKey, result);
			return result;
		}
	}
}