using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using DanSerialiser;

namespace UnitTests
{
	public class PersistentListTypeConverter : ISerialisationTypeConverter, IDeserialisationTypeConverter
	{
		/// <summary>
		/// When considering type converters for serialisation, there is only one factor to consider - what the current type of the value being considered is; if it's a PersistentList
		/// then we want to transform it into an array
		/// </summary>
		private static ConcurrentDictionary<Type, Func<object, object>> _serialisationConverters = new ConcurrentDictionary<Type, Func<object, object>>();

		/// <summary>
		/// When considering type converters for DEserialisation, there are two factors to consider - the current type of the value and the target type; if the current type is an array
		/// with element T and the target type of a PersistentList of T then we want to transform that array back into a Persistent list (this is why the cache key here has two types
		/// but the cache key for _serialisationConverters is a single type)
		/// </summary>
		private static ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>> _deserialisationConverters = new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>>();

		public static PersistentListTypeConverter Instance { get; } = new PersistentListTypeConverter();
		private PersistentListTypeConverter() { }

		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			if (value == null)
				return null;

			// Note: Depending upon the shape of the data that is being serialised, there may be some minor performance improvements available by rearranging the type checks and the
			// dictionary lookups below but I suspect that any gains would be small and it would require profiling to ensure that they feasible - without more information, it seems
			// best to start with something sensible and then chase any last few percentage improvements after proving that it's worthwhile looking here, specifically
			var type = value.GetType();
			if (!type.IsGenericType || (type.GetGenericTypeDefinition() != typeof(PersistentList<>)))
				return value;
			if (_serialisationConverters.TryGetValue(type, out var cachedTransformer))
				return cachedTransformer(value);

			var sourceParameter = Expression.Parameter(typeof(object), "source");
			var transformer =
				Expression.Lambda<Func<object, object>>(
					Expression.Call(
						Expression.Convert(sourceParameter, type),
						type.GetMethod("ToArray")
					),
					sourceParameter
				)
				.Compile();
			_serialisationConverters.TryAdd(type, transformer);
			return transformer(value);
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (value == null)
				return null;

			// Note: See comments in above method - there MAY be minor performance improvements possible by rearranging these type checks / dictionary lookups but it would require
			// profiling (ideally with real world data) to be sure (and it might vary from platform to platform, framework to framework)
			if (!targetType.IsGenericType || (targetType.GetGenericTypeDefinition() != typeof(PersistentList<>)))
				return value;
			var targetListElementType = targetType.GetGenericArguments()[0];
			var currentType = value.GetType();
			if (!currentType.IsArray || (currentType.GetElementType() != targetListElementType))
				return value;
			var cacheKey = Tuple.Create(targetType, currentType);
			if (_deserialisationConverters.TryGetValue(cacheKey, out var cachedTransformer))
				return cachedTransformer(value);

			var sourceParameter = Expression.Parameter(typeof(object), "source");
			var transformer =
				Expression.Lambda<Func<object, object>>(
					Expression.Call(
						typeof(PersistentList).GetMethod(nameof(PersistentList.Of)).MakeGenericMethod(targetListElementType),
						Expression.Convert(sourceParameter, currentType)
					),
					sourceParameter
				)
				.Compile();
			_deserialisationConverters.TryAdd(cacheKey, transformer);
			return transformer(value);
		}
	}
}