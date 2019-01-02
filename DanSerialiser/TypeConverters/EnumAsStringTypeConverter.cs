using System;
using System.Collections.Concurrent;
using System.Linq;

namespace DanSerialiser
{
	/// <summary>
	/// The default behaviour for de/serialisation if enums is to record the underlying numeric value in the serialised data. This means that is is important not
	/// to reorder enum names between versions of assemblies, otherwise the underlying value 1 may map to different enum values in different assembly versions.
	/// One alternative is to use this type converter when serialising and deserialising so that the enum names are written as strings in the serialised data.
	/// This will be a little slower as strings encoding is more expensive than the encoding of numeric types that enums may back on to and the strings will
	/// require more bytes to be written.
	/// </summary>
	public sealed class EnumAsStringTypeConverter : ISerialisationTypeConverter, IDeserialisationTypeConverter
	{
		private static readonly ConcurrentDictionary<Type, Func<object, string>> _toStringLookups = new ConcurrentDictionary<Type, Func<object, string>>();
		private static readonly ConcurrentDictionary<Type, Func<string, object>> _fromStringLookups = new ConcurrentDictionary<Type, Func<string, object>>();

		public static EnumAsStringTypeConverter Instance { get; } = new EnumAsStringTypeConverter();
		private EnumAsStringTypeConverter() { }

		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			if (value == null)
				return value;

			var valueType = value.GetType();
			if (!valueType.IsEnum)
				return value;

			if (!_toStringLookups.TryGetValue(valueType, out var nameLookup))
			{
				var nameLookupDictionary = Enum.GetValues(valueType)
					.Cast<object>()
					.Zip(Enum.GetNames(valueType), Tuple.Create)
					.ToDictionary(entry => entry.Item1, entry => entry.Item2);
				nameLookup = enumValue => nameLookupDictionary.TryGetValue(enumValue, out var name) ? name : ""; // Set invalid values to "" so that they can be successfully round-tripped
				_toStringLookups.TryAdd(valueType, nameLookup);
			}
			return nameLookup(value);
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (!targetType.IsEnum || !(value is string valueString))
				return value;

			if (!_fromStringLookups.TryGetValue(targetType, out var valueLookup))
			{
				var valueLookupDictionary = Enum.GetNames(targetType)
					.Zip(Enum.GetValues(targetType).Cast<object>(), Tuple.Create)
					.ToDictionary(entry => entry.Item1, entry => entry.Item2);
				valueLookup = name => valueLookupDictionary.TryGetValue(name, out var enumValue) ? enumValue : Activator.CreateInstance(targetType);
				_fromStringLookups.TryAdd(targetType, valueLookup);
			}
			return valueLookup(valueString);
		}
	}
}