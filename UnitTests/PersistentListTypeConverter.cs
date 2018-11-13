using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using DanSerialiser;

namespace UnitTests
{
	/// <summary>
	/// This test class illustrates how a type converter may be defined for a particular generic type - this makes it more complicated than the EnumAsStringTypeConverter
	/// but less complicated than the ImmutableListTypeConverter (which goes in for a form of duck typing - because it looks for a particular type 'shape' rather than any
	/// particular type name - AND dealing with generic types)
	/// </summary>
	internal class PersistentListTypeConverter : ISerialisationTypeConverter, IDeserialisationTypeConverter
	{
		private delegate object Transformer(object value, Type targetTypeIfDeserialising);
		private static readonly ConcurrentDictionary<Type, Transformer> _serialisationConverters = new ConcurrentDictionary<Type, Transformer>();

		public static PersistentListTypeConverter Instance { get; } = new PersistentListTypeConverter();
		private PersistentListTypeConverter() { }

		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			if (value == null)
				return null;

			var type = value.GetType();
			if (!type.IsGenericType || (type.GetGenericTypeDefinition() != typeof(PersistentList<>)))
				return value;

			var elementType = type.GetGenericArguments()[0];
			var transformer = GetTransformer(elementType);
			return transformer(value, targetTypeIfDeserialising: null);
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (value == null)
				return null;

			if (!targetType.IsGenericType || (targetType.GetGenericTypeDefinition() != typeof(PersistentList<>)))
				return value;

			var elementType = targetType.GetGenericArguments()[0];
			var transformer = GetTransformer(elementType);
			return transformer(value, targetType);
		}

		private static object ConvertIfRequired<T>(object value, Type targetTypeIfDeserialising)
		{
			if (value != null)
			{
				// If targetTypeIfDeserialising is null then this is a ConvertIfRequired during serialisation
				if (targetTypeIfDeserialising == null)
					return ((value is PersistentList<T> list)) ? list.ToArray() : value;

				if (targetTypeIfDeserialising == typeof(PersistentList<T>))
					return ((value is IEnumerable<T> enumerable)) ? PersistentList.Of(enumerable) : value;
			}
			return value;
		}

		private Transformer GetTransformer(Type type) => _serialisationConverters.GetOrAdd(type, BuildTransformer);

		private Transformer BuildTransformer(Type type)
		{
			var genericConvertIfRequiredMethod = GetType().GetMethod(nameof(ConvertIfRequired), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(type);
			var sourceParameter = Expression.Parameter(typeof(object), "source");
			var targetTypeIfDeserialisingParameter = Expression.Parameter(typeof(Type), "targetTypeIfDeserialising");
			return
				Expression.Lambda<Transformer>(
					Expression.Call(
						genericConvertIfRequiredMethod,
						sourceParameter,
						targetTypeIfDeserialisingParameter
					),
					sourceParameter,
					targetTypeIfDeserialisingParameter
				)
				.Compile();
		}
	}
}