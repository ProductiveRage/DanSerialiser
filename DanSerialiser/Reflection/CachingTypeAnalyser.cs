using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class CachingTypeAnalyser : IAnalyseTypesForSerialisation
	{
		private readonly IAnalyseTypesForSerialisation _reader;
		private readonly ConcurrentDictionary<string, Type> _typeLookupCache;
		private readonly ConcurrentDictionary<string, Func<object>> _typeBuilderCache;
		private readonly ConcurrentDictionary<Type, Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]>> _fieldAndPropertyCache;
		private readonly ConcurrentDictionary<Type, FieldInfo[]> _requiredFieldCache;
		private readonly ConcurrentDictionary<Tuple<Type, string, string>, MemberAndWriter<FieldInfo>> _fieldNameCache;
		private readonly ConcurrentDictionary<Tuple<Type, string, string, Type>, DeprecatedPropertySettingDetails> _deprecatedPropertyCache;
		public CachingTypeAnalyser(IAnalyseTypesForSerialisation reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
			_typeLookupCache = new ConcurrentDictionary<string, Type>();
			_typeBuilderCache = new ConcurrentDictionary<string, Func<object>>();
			_fieldAndPropertyCache = new ConcurrentDictionary<Type, Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]>>();
			_requiredFieldCache = new ConcurrentDictionary<Type, FieldInfo[]>();
			_fieldNameCache = new ConcurrentDictionary<Tuple<Type, string, string>, MemberAndWriter<FieldInfo>>();
			_deprecatedPropertyCache = new ConcurrentDictionary<Tuple<Type, string, string, Type>, DeprecatedPropertySettingDetails>();
		}

		/// <summary>
		/// If unable to resolve the type, this will throw an exception when ignoreAnyInvalidTypes is false; otherwise return null when it's true.
		/// </summary>
		public Type GetType(string typeName, bool ignoreAnyInvalidTypes)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			if (_typeLookupCache.TryGetValue(typeName, out var cachedResult))
				return cachedResult;

			return _typeLookupCache.GetOrAdd(typeName, _reader.GetType(typeName, ignoreAnyInvalidTypes));
		}

		public Func<object> TryToGetUninitialisedInstanceBuilder(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			if (_typeBuilderCache.TryGetValue(typeName, out var cachedResult))
				return cachedResult;

			return _typeBuilderCache.GetOrAdd(typeName, _reader.TryToGetUninitialisedInstanceBuilder(typeName));
		}

		public Tuple<MemberAndReader<FieldInfo>[], MemberAndReader<PropertyInfo>[]> GetFieldsAndProperties(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_fieldAndPropertyCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			return _fieldAndPropertyCache.GetOrAdd(type, _reader.GetFieldsAndProperties(type));
		}

		public FieldInfo[] GetAllFieldsThatShouldBeSet(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_requiredFieldCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			return _requiredFieldCache.GetOrAdd(type, _reader.GetAllFieldsThatShouldBeSet(type));
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

			return _fieldNameCache.GetOrAdd(cacheKey, _reader.TryToFindField(type, fieldName, specificTypeNameIfRequired));
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

			return _deprecatedPropertyCache.GetOrAdd(cacheKey, _reader.TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(typeToLookForPropertyOn, fieldName, typeNameIfRequired, fieldValueTypeIfAvailable));
		}
	}
}