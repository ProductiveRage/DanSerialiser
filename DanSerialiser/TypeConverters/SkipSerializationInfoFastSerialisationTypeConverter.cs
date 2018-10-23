using System;
using System.Runtime.Serialization;

namespace DanSerialiser
{
	/// <summary>
	/// In order for the FastestTreeBinarySerialisation to apply as many optimisations as possible, there must not be any 'unknown' types present in the object model - an unknown type is
	/// a non-sealed class or an interface because a property of type non-sealed MyClass MAY have an instance of MyClass when it comes to be serialised or it may have have an instance of
	/// MyOtherClass, which is derived from MyClass. The same applies to interfaces because there is no way to know what type will used to implement that interface. It is common amongst
	/// classes in the base library (the HashSet, for example) to have a SerializationInfo field that is set via a constructor intended for use on ISerializable implementations that is
	/// not marked with the NonSerialized attribute (though it probably should be). As the SerializationInfo has a IFormatterConverter field, the FastestTreeBinarySerialisation will not
	/// be able to apply all optimisations to such types unless this type converter is used, which will record a null value for a SerializationInfo field or property in the serialised
	/// data.
	/// </summary>
	public sealed class SkipSerializationInfoFastSerialisationTypeConverter : IFastSerialisationTypeConverter
	{
		public static SkipSerializationInfoFastSerialisationTypeConverter Instance { get; } = new SkipSerializationInfoFastSerialisationTypeConverter();
		private SkipSerializationInfoFastSerialisationTypeConverter() { }

		object ISerialisationTypeConverter.ConvertIfRequired(object value) => (value is SerializationInfo) ? null : value;

		FastSerialisationTypeConversionResult IFastSerialisationTypeConverter.GetDirectWriterIfPossible(Type sourceType, MemberSetterDetailsRetriever memberSetterDetailsRetriever)
		{
			if (sourceType == null)
				throw new ArgumentNullException(nameof(sourceType));
			if (memberSetterDetailsRetriever == null)
				throw new ArgumentNullException(nameof(memberSetterDetailsRetriever));

			return (sourceType == typeof(SerializationInfo))
				? FastSerialisationTypeConversionResult.SetToDefault(sourceType)
				: null;
		}
	}
}