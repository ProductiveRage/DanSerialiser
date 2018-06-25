using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DanSerialiser
{
	public sealed class BinarySerialisationWriter : IWrite
	{
		private readonly Stream _stream;
		private readonly Dictionary<Type, byte[]> _typeNameCache;
		private readonly Dictionary<Tuple<FieldInfo, Type>, byte[]> _fieldNameCache;
		private readonly Dictionary<PropertyInfo, byte[]> _propertyNameCache;
		public BinarySerialisationWriter(Stream stream, bool supportReferenceReuse)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			SupportReferenceReuse = supportReferenceReuse;
			_typeNameCache = new Dictionary<Type, byte[]>();
			_fieldNameCache = new Dictionary<Tuple<FieldInfo, Type>, byte[]>();
			_propertyNameCache = new Dictionary<PropertyInfo, byte[]>();
		}

		public bool SupportReferenceReuse { get; }

		public void Boolean(bool value)
		{
			WriteByte((byte)BinarySerialisationDataType.Boolean);
			WriteByte(value ? (byte)1 : (byte)0);
		}
		public void Byte(byte value)
		{
			WriteByte((byte)BinarySerialisationDataType.Byte);
			WriteByte(value);
		}
		public void SByte(sbyte value)
		{
			WriteByte((byte)BinarySerialisationDataType.SByte);
			WriteByte((byte)value);
		}

		public void Int16(short value)
		{
			WriteByte((byte)BinarySerialisationDataType.Int16);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void Int32(int value)
		{
			WriteByte((byte)BinarySerialisationDataType.Int32);
			IntWithoutDataType(value);
		}
		public void Int64(long value)
		{
			WriteByte((byte)BinarySerialisationDataType.Int64);
			WriteBytes(BitConverter.GetBytes(value));
		}

		public void Single(float value)
		{
			WriteByte((byte)BinarySerialisationDataType.Single);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void Double(double value)
		{
			WriteByte((byte)BinarySerialisationDataType.Double);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void Decimal(decimal value)
		{
			// BitConverter's "GetBytes" method doesn't support decimal so use "decimal.GetBits" that returns four int values
			WriteByte((byte)BinarySerialisationDataType.Decimal);
			foreach (var partialValue in decimal.GetBits(value))
				IntWithoutDataType(partialValue);
		}

		public void UInt16(ushort value)
		{
			WriteByte((byte)BinarySerialisationDataType.UInt16);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void UInt32(uint value)
		{
			WriteByte((byte)BinarySerialisationDataType.UInt32);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void UInt64(ulong value)
		{
			WriteByte((byte)BinarySerialisationDataType.UInt64);
			WriteBytes(BitConverter.GetBytes(value));
		}

		public void Char(char value)
		{
			WriteByte((byte)BinarySerialisationDataType.Char);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void String(string value)
		{
			WriteByte((byte)BinarySerialisationDataType.String);
			StringWithoutDataType(value);
		}

		public void ArrayStart(object value, Type elementType)
		{
			if (elementType == null)
				throw new ArgumentNullException(nameof(elementType));
			if ((value != null) && !(value is Array))
				throw new ArgumentException($"If {nameof(value)} is not null then it must be an array");

			WriteByte((byte)BinarySerialisationDataType.ArrayStart);
			WriteTypeName((value == null) ? null : elementType);
			if (value != null)
			{
				// If the value is null then WriteTypeName will have written a representation of a null string and the BinaryReader will understand that this represents a
				// null value (and so we don't need to write any length data here)
				IntWithoutDataType(((Array)(object)value).Length);
			}
		}

		public void ArrayEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ArrayEnd);
		}

		public void ObjectStart<T>(T value)
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectStart);
			WriteTypeName(value?.GetType());
		}

		public void ObjectEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectEnd);
		}

		public void ReferenceId(int value)
		{
			WriteByte((byte)BinarySerialisationDataType.ReferenceID);
			IntWithoutDataType(value);
		}

		public bool FieldName(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			return WriteFieldNameBytesIfWantoSerialiseField(field, serialisationTargetType);
		}

		public bool PropertyName(PropertyInfo property, Type serialisationTargetType)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			return WritePropertyNameBytesIfWantoSerialiseField(property);
		}

		private void WriteTypeName(Type typeIfValueIsNotNull)
		{
			// When recording a type name, either write a null string for it OR write a string and then the Name Reference ID that that string should be stored as OR write just
			// the Name Reference ID (if the type name has already been recorded once and may be reused)
			if (typeIfValueIsNotNull == null)
			{
				WriteByte((byte)BinarySerialisationDataType.String);
				StringWithoutDataType(null);
				return;
			}

			if (_typeNameCache.TryGetValue(typeIfValueIsNotNull, out var cachedResult))
			{
				WriteBytes(cachedResult);
				return;
			}

			// This is first time that the type has been encountered and so we need to write the full string and the Name Reference ID but the bytes in the cache will just be a
			// point to a NameReferenceID (so next time the type is encountered, ONLY that ID will be written)
			var nextReferenceID = GetNextReferenceID();
			WriteByte((byte)BinarySerialisationDataType.String);
			StringWithoutDataType(typeIfValueIsNotNull.AssemblyQualifiedName);
			IntWithoutDataType(nextReferenceID);
			_typeNameCache[typeIfValueIsNotNull] = new[] { (byte)BinarySerialisationDataType.NameReferenceID }.Concat(BitConverter.GetBytes(nextReferenceID)).ToArray();
		}

		private bool WriteFieldNameBytesIfWantoSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			var cacheKey = Tuple.Create(field, serialisationTargetType);
			if (_fieldNameCache.TryGetValue(cacheKey, out var cachedResult))
			{
				if (cachedResult == null)
					return false;
				WriteBytes(cachedResult);
				return true;
			}

			if (BinaryReaderWriterShared.IgnoreField(field))
			{
				_fieldNameCache[cacheKey] = null;
				return false;
			}

			// Serialisation of pointer fields will fail - I don't know how they would be supportable anyway but they fail with a stack overflow if attempted, so catch it
			// first and raise as a more useful exception
			if (field.FieldType.IsPointer || (field.FieldType == typeof(IntPtr)) || (field.FieldType == typeof(UIntPtr)))
				throw new NotSupportedException($"Can not serialise pointer fields: {field.Name} on {field.DeclaringType.Name}");

			// If a field is declared multiple times in the type hierarchy (whether through overrides or use of "new") then its name will need prefixing with the type
			// that this FieldInfo relates to
			var fieldNameExistsMultipleTimesInHierarchy = false;
			var currentType = serialisationTargetType;
			while (currentType != null)
			{
				if (currentType != serialisationTargetType)
				{
					if (currentType.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags).Any(f => f.Name == field.Name))
					{
						fieldNameExistsMultipleTimesInHierarchy = true;
						break;
					}
				}
				currentType = currentType.BaseType;
			}

			// When recording a field name, either write a string and then the Name Reference ID that that string should be stored as OR write just the Name Reference ID
			// (if the field name has already been recorded once and may be reused)
			var nextReferenceID = GetNextReferenceID();
			WriteByte((byte)BinarySerialisationDataType.FieldName);
			String(BinaryReaderWriterShared.CombineTypeAndFieldName(fieldNameExistsMultipleTimesInHierarchy ? field.DeclaringType.AssemblyQualifiedName : null, field.Name));
			IntWithoutDataType(nextReferenceID);
			_fieldNameCache[cacheKey] = new[] { (byte)BinarySerialisationDataType.FieldName, (byte)BinarySerialisationDataType.NameReferenceID }.Concat(BitConverter.GetBytes(nextReferenceID)).ToArray();
			return true;
		}

		private bool WritePropertyNameBytesIfWantoSerialiseField(PropertyInfo property)
		{
			if (_propertyNameCache.TryGetValue(property, out var cachedResult))
			{
				if (cachedResult == null)
					return false;
				WriteBytes(cachedResult);
				return true;
			}

			// Most of the time, we'll just serialise the backing fields because that should capture all of the data..
			if (property.GetCustomAttribute<DeprecatedAttribute>() == null)
			{
				_propertyNameCache[property] = null;
				return false;
			}

			if (property.PropertyType.IsPointer || (property.PropertyType == typeof(IntPtr)) || (property.PropertyType == typeof(UIntPtr)))
				throw new NotSupportedException($"Can not serialise pointer properties: {property.Name} on {property.DeclaringType.Name}");

			// .. however, if this is a property that has the [Deprecated] attribute on it then it is expected to exist for backwards compatibility and to be a computed property
			// (and so have no backing field) but one that we want to include in the serialised data anyway. If V1 of a type has a string "Name" property which is replaced in V2
			// with a "TranslatedName" property of type TranslatedString then a computed "Name" property could be added to the V2 type (annotated with [Deprecated]) whose getter
			// returns the default language value of the TranslatedName - this value may then be included in the serialisation data so that an assembly that has loaded the V1
			// type can deserialise and populate its Name property.
			// - Note: We won't try to determine whether or not the type name prefix is necessary when recording the field name because the type hierarchy and the properties on
			//   them might be different now than in the version of the types where deserialisation occurs so the type name will always be inserted before the field name to err
			//   on the safe side
			// - Further note: Similar approach to type and field name recording is taken here; the first time a property is written, the string is serialised, while subsequent
			//   times get a NameReferenceID instead
			var nextReferenceID = GetNextReferenceID();
			WriteByte((byte)BinarySerialisationDataType.FieldName); // Even though it's a property, we're stashing it using the backing field name
			String(BinaryReaderWriterShared.CombineTypeAndFieldName(property.DeclaringType.AssemblyQualifiedName,BackingFieldHelpers.GetBackingFieldName(property.Name)));
			IntWithoutDataType(nextReferenceID);
			_propertyNameCache[property] = new[] { (byte)BinarySerialisationDataType.FieldName, (byte)BinarySerialisationDataType.NameReferenceID }.Concat(BitConverter.GetBytes(nextReferenceID)).ToArray();
			return true;
		}

		private int GetNextReferenceID()
		{
			return _typeNameCache.Count + _fieldNameCache.Count + _propertyNameCache.Count;
		}

		private void IntWithoutDataType(int value)
		{
			WriteBytes(BitConverter.GetBytes(value));
		}

		private void StringWithoutDataType(string value)
		{
			WriteBytes(GetStringBytes(value));
		}

		private byte[] GetStringBytes(string value)
		{
			if (value == null)
				return BitConverter.GetBytes(-1);
			var bytes = Encoding.UTF8.GetBytes(value);
			var combinedContent = new byte[bytes.Length + 4];
			Array.Copy(BitConverter.GetBytes(bytes.Length), combinedContent, length:  4);
			Array.Copy(bytes, sourceIndex: 0, destinationArray: combinedContent, destinationIndex: 4, length: bytes.Length);
			return combinedContent;
		}

		private void WriteByte(byte value)
		{
			_stream.WriteByte(value);
		}

		private void WriteBytes(byte[] value)
		{
			_stream.Write(value, 0, value.Length);
		}
	}
}
 