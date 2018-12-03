using System;
using System.Linq.Expressions;

namespace DanSerialiser // Note that this is not in the TypeConverters namespace because it's needs part of the public API (and I would prefer for consumers to only need a single "using DanSerialiser")
{
	public sealed class FastSerialisationTypeConversionResult
	{
		/// <summary>
		/// When converting to an interim type for serialisation using an IFastSerialisationTypeConverter, the target 'convertedToType' may not be an abstract class or
		/// an interface (because it would not be possible for the deserialiser to know what type to deserialise back into) and it may not be an array type (due to the
		/// way in which arrays are represented internally within the serialisation process) - if it would be desirable to convert a type into an array then it will be
		/// necessary to wrap that array in a sealed class (and the deserialisation type converter would need to be aware of this).
		/// </summary>
		public static FastSerialisationTypeConversionResult ConvertTo(Type type, Type convertedToType, LambdaExpression memberSetter)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (convertedToType == null)
				throw new ArgumentNullException(nameof(convertedToType));
			if (convertedToType == typeof(void))
				throw new ArgumentException($"may not be typeof(void)", nameof(convertedToType));
			if (convertedToType.IsArray)
				throw new ArgumentNullException("must be an array", nameof(convertedToType));
			if (convertedToType.IsInterface)
				throw new ArgumentNullException("must be a non-abstract class, not an interface", nameof(convertedToType));
			if (convertedToType.IsAbstract)
				throw new ArgumentNullException("must be a non-abstract class", nameof(convertedToType));
			if (memberSetter == null)
				throw new ArgumentNullException(nameof(memberSetter));
			if ((memberSetter.Parameters.Count != 2)
			|| (memberSetter.Parameters[0].Type != type)
			|| (memberSetter.Parameters[1].Type != typeof(BinarySerialisationWriter))
			|| (memberSetter.ReturnType != typeof(void)))
				throw new ArgumentException($"The {nameof(memberSetter)} lambda expression must have two parameters - {type} and {nameof(BinarySerialisationWriter)} - and void return type");

			return new FastSerialisationTypeConversionResult(type, convertedToType, memberSetter);
		}

		/// <summary>
		/// Use this FastSerialisationTypeConversionResult factory method to indicate that a default(T) value should always be written - this doesn't change the type,
		/// it just changes how the serialisation of it is handled (by recording a default value and ignore any actual data)
		/// </summary>
		public static FastSerialisationTypeConversionResult SetToDefault(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			// If recording a default(T) value for the type then the type will not change and so "convertedType" will be the same as "type"
			return new FastSerialisationTypeConversionResult(type, type, memberSetterIfNotSettingToDefault: null);
		}

		private FastSerialisationTypeConversionResult(Type type, Type convertedToType, LambdaExpression memberSetterIfNotSettingToDefault)
		{
			Type = type ?? throw new ArgumentNullException(nameof(type));
			ConvertedToType = convertedToType ?? throw new ArgumentNullException(nameof(convertedToType));
			MemberSetterIfNotSettingToDefault = memberSetterIfNotSettingToDefault;
		}

		public Type Type { get; }

		/// <summary>
		/// This will never be an abstract class type or an interface or an array - the first two because it would not be possible for the deserialiser to know what
		/// type to deserialise into and the last one due to how the Serialiser represents arrays internally (which is related to its reference tracking support)
		/// </summary>
		public Type ConvertedToType { get; }

		/// <summary>
		/// This LambdaExpression will have two parameters, the first matching the Type property of the instance and the second being of type BinarySerialisationWriter.
		/// It will have no return type. When the lambda is invoked, the field and property values for the instance of the Type will be serialised using the specified
		/// BinarySerialisationWriter. It will be null if this instance was created via the SetToDefault was used because no members will be set in that case.
		/// </summary>
		public LambdaExpression MemberSetterIfNotSettingToDefault { get; }
	}
}