using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace DanSerialiser
{
	/// <summary>
	/// This library can not directly serialise instances of Types as it does not support serialisation of pointer fields and these are present in Type data. If
	/// Type instances need to be serialised then this type converter may be used (it writes the Type's AssemblyQualifiedName as a string instead of attempting to
	/// investigate the full structure of the Type class and it will call Type.GetType to deserialise back from the string value).
	/// </summary>
	public sealed class TypeTypeConverter : IFastSerialisationTypeConverter, IDeserialisationTypeConverter
	{
		private static readonly ConcurrentDictionary<Type, Func<object, string>> _toStringLookups = new ConcurrentDictionary<Type, Func<object, string>>();
		private static readonly ConcurrentDictionary<Type, Func<string, object>> _fromStringLookups = new ConcurrentDictionary<Type, Func<string, object>>();

		public static TypeTypeConverter Instance { get; } = new TypeTypeConverter();
		private TypeTypeConverter() { }

		private static readonly MethodInfo _stringWriteMethod = typeof(BinarySerialisationWriter).GetMethod(nameof(BinarySerialisationWriter.String));
		private static readonly PropertyInfo _assemblyQualifiedNameProperty = typeof(Type).GetProperty(nameof(Type.AssemblyQualifiedName));
		FastSerialisationTypeConversionResult IFastSerialisationTypeConverter.GetDirectWriterIfPossible(Type sourceType, MemberSetterDetailsRetriever memberSetterDetailsRetriever)
		{
			if (sourceType == null)
				throw new ArgumentNullException(nameof(sourceType));
			if (memberSetterDetailsRetriever == null)
				throw new ArgumentNullException(nameof(memberSetterDetailsRetriever));

			if (!typeof(Type).IsAssignableFrom(sourceType))
				return null;

			var sourceParameter = Expression.Parameter(sourceType, "source");
			var writerParameter = Expression.Parameter(typeof(BinarySerialisationWriter), "writer");
			return FastSerialisationTypeConversionResult.ConvertTo(
				sourceType,
				typeof(string),
				Expression.Lambda(
					Expression.Call(
						writerParameter,
						_stringWriteMethod,
						Expression.Property(sourceParameter, _assemblyQualifiedNameProperty)
					),
					sourceParameter,
					writerParameter
				)
			);
		}

		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			var type = value as Type;
			if (type == null)
				return value;

			return type.AssemblyQualifiedName;
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if ((targetType != typeof(Type)) || !(value is string valueString))
				return value;

			return Type.GetType(valueString);
		}
	}
}