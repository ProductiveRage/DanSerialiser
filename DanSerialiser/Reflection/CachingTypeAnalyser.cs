using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class CachingTypeAnalyser : IAnalyseTypesForSerialisation
	{
		private readonly IAnalyseTypesForSerialisation _reader;
		private readonly ConcurrentDictionary<Type, Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>> _fieldAndPropertyCache;
		public CachingTypeAnalyser(IAnalyseTypesForSerialisation reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
			_fieldAndPropertyCache = new ConcurrentDictionary<Type, Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>>();
		}

		public Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>> GetFieldsAndProperties(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_fieldAndPropertyCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			var result = _reader.GetFieldsAndProperties(type);
			_fieldAndPropertyCache.TryAdd(type, result);
			return result;
		}
	}
}