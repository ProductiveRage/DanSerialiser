using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	public sealed class BinarySerialisationWriter : IWrite
	{
		private readonly Stream _stream;
		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		private readonly Dictionary<Type, BinarySerialisationWriterCachedNames.CachedNameData> _recordedTypeNames;
		private readonly Dictionary<Tuple<FieldInfo, Type>, BinarySerialisationWriterCachedNames.CachedNameData> _encounteredFields;
		private readonly Dictionary<PropertyInfo, BinarySerialisationWriterCachedNames.CachedNameData> _encounteredProperties;
		public BinarySerialisationWriter(Stream stream, bool supportReferenceReuse) : this(stream, supportReferenceReuse, DefaultTypeAnalyser.Instance) { }
		internal BinarySerialisationWriter(Stream stream, bool supportReferenceReuse, IAnalyseTypesForSerialisation typeAnalyser) // internal constructor for unit testing
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			SupportReferenceReuse = supportReferenceReuse;
			_typeAnalyser = typeAnalyser;

			_recordedTypeNames = new Dictionary<Type, BinarySerialisationWriterCachedNames.CachedNameData>();
			_encounteredFields = new Dictionary<Tuple<FieldInfo, Type>, BinarySerialisationWriterCachedNames.CachedNameData>();
			_encounteredProperties = new Dictionary<PropertyInfo, BinarySerialisationWriterCachedNames.CachedNameData>();
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
			Int16WithoutDataType(value);
		}
		public void Int32(int value)
		{
			if ((value >= byte.MinValue) && (value <= byte.MaxValue))
			{
				WriteByte((byte)BinarySerialisationDataType.Int32_Byte);
				WriteByte((byte)value);
			}
			else if ((value >= short.MinValue) && (value <= short.MaxValue))
			{
				WriteByte((byte)BinarySerialisationDataType.Int32_Int16);
				Int16WithoutDataType((short)value);
			}
			else
			{
				WriteByte((byte)BinarySerialisationDataType.Int32);
				Int32WithoutDataType(value);
			}
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
				Int32WithoutDataType(partialValue);
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
				Int32WithoutDataType(((Array)(object)value).Length);
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
			Int32WithoutDataType(value);
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

		public Action<object> TryToGenerateMemberSetter(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var memberSetter = SharedGeneratedMemberSetters.TryToGenerateMemberSetter(type);
			if (memberSetter == null)
				return null;
			return value => memberSetter(value, this);
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

			if (_recordedTypeNames.TryGetValue(typeIfValueIsNotNull, out var cachedData))
			{
				// If we've encountered this field before then we return the bytes for the Name Reference ID only
				WriteBytes(cachedData.OnlyAsReferenceID);
				return;
			}

			// If we haven't encountered this type before then we'll need to write out the full string data (if another write has encountered this type then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this type - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetTypeNameBytes(typeIfValueIsNotNull);
			_recordedTypeNames[typeIfValueIsNotNull] = cachedData;
			WriteBytes(cachedData.AsStringAndReferenceID);
		}

		private bool WriteFieldNameBytesIfWantoSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			var fieldOnType = Tuple.Create(field, serialisationTargetType);
			if (_encounteredFields.TryGetValue(fieldOnType, out var cachedData))
			{
				// If we've encountered this field before then we return the bytes for the Name Reference ID only (unless we've got a null value, which means skip it and return
				// null from here)
				if (cachedData == null)
					return false;

				WriteByte((byte)BinarySerialisationDataType.FieldName);
				WriteBytes(cachedData.OnlyAsReferenceID);
				return true;
			}

			// If we haven't encountered this field before then we'll need to write out the full string data (if another write has encountered this field then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this field - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseField(field, serialisationTargetType);
			_encounteredFields[fieldOnType] = cachedData;
			if (cachedData == null)
				return false;

			WriteByte((byte)BinarySerialisationDataType.FieldName);
			WriteBytes(cachedData.AsStringAndReferenceID);
			return true;
		}

		private bool WritePropertyNameBytesIfWantoSerialiseField(PropertyInfo property)
		{
			if (_encounteredProperties.TryGetValue(property, out var cachedData))
			{
				// If we've encountered this property before then we return the bytes for the Name Reference ID only (unless we've got a null value, which means skip it and
				// return null from here)
				if (cachedData == null)
					return false;

				WriteByte((byte)BinarySerialisationDataType.FieldName);
				WriteBytes(cachedData.OnlyAsReferenceID);
				return true;
			}

			// If we haven't encountered this field before then we'll need to write out the full string data (if another write has encountered this field then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this field - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseProperty(property);
			_encounteredProperties[property] = cachedData;
			if (cachedData == null)
				return false;

			WriteByte((byte)BinarySerialisationDataType.FieldName);
			WriteBytes(cachedData.AsStringAndReferenceID);
			return true;
		}

		internal void Int32WithoutDataType(int value)
		{
			WriteByte((byte)(value >> 24));
			WriteByte((byte)(value >> 16));
			WriteByte((byte)(value >> 8));
			WriteByte((byte)value);
		}

		private void Int16WithoutDataType(short value)
		{
			WriteByte((byte)(value >> 8));
			WriteByte((byte)value);
		}

		private byte[] GetBytesForInt32WithoutDataType(int value)
		{
			var bytes = new byte[4];
			bytes[0] = (byte)(value >> 24);
			bytes[1] = (byte)(value >> 16);
			bytes[2] = (byte)(value >> 8);
			bytes[3] = (byte)value;
			return bytes;
		}

		private void StringWithoutDataType(string value)
		{
			if (value == null)
			{
				Int32WithoutDataType(-1);
				return;
			}

			var bytes = Encoding.UTF8.GetBytes(value);
			Int32WithoutDataType(bytes.Length);
			WriteBytes(bytes);
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