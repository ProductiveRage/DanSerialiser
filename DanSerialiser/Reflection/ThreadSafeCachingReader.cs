using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class ThreadSafeCachingReader : IReadValues
	{
		private readonly IReadValues _reader;
		private ImmutableDictionary<Type, Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>> _fieldAndPropertyCache;
		public ThreadSafeCachingReader(IReadValues reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
			_fieldAndPropertyCache = ImmutableDictionary<Type, Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>>>.Empty;
		}

		public Tuple<IEnumerable<MemberAndReader<FieldInfo>>, IEnumerable<MemberAndReader<PropertyInfo>>> GetFieldsAndProperties(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_fieldAndPropertyCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			var result = _reader.GetFieldsAndProperties(type);
			_fieldAndPropertyCache = _fieldAndPropertyCache.SetItem(type, result);
			return result;
		}
	}
}