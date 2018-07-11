using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DanSerialiser.BinaryTypeStructures;
using DanSerialiser.CachedLookups;
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
		public BinarySerialisationWriter(Stream stream, bool supportReferenceReuse = true) : this(stream, supportReferenceReuse, DefaultTypeAnalyser.Instance) { }
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
			WriteBytes((byte)BinarySerialisationDataType.Boolean, value ? (byte)1 : (byte)0);
		}
		public void Byte(byte value)
		{
			WriteBytes((byte)BinarySerialisationDataType.Byte, value);
		}
		public void SByte(sbyte value)
		{
			WriteBytes((byte)BinarySerialisationDataType.SByte, (byte)value);
		}

		public void Int16(short value)
		{
			WriteBytes((new Int16Bytes(value)).GetLittleEndianBytesWithDataType(BinarySerialisationDataType.Int16));
		}
		public void Int32(int value)
		{
			VariableLengthInt32(value, BinarySerialisationDataType.Int32_8, BinarySerialisationDataType.Int32_16, BinarySerialisationDataType.Int32_24, BinarySerialisationDataType.Int32);
		}
		public void Int64(long value)
		{
			WriteByte((byte)BinarySerialisationDataType.Int64);
			Int64WithoutDataType(value);
		}
		public void Single(float value)
		{
			WriteByte((byte)BinarySerialisationDataType.Single);
			WriteBytes(BitConverter.GetBytes(value));
		}
		public void Double(double value)
		{
			WriteBytes((new DoubleBytes(value)).GetLittleEndianBytesWithDataType());
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

		// There's nothing inherently special or awkward about DateTime that the Serialiser couldn't serialise it like any other type (by recording all of its fields' values) but it's
		// such a common type that it seems like optimising it a little wouldn't hurt AND having it as an IWrite method means that the SharedGeneratedMemberSetters can make use of it,
		// which broadens the range of types that it can generate (and that makes things faster when the same types are serialised over and over again)
		public void DateTime(DateTime value)
		{
			// "under the hood a .NET DateTime is essentially a tick count plus a DateTimeKind"
			// - "http://mark-dot-net.blogspot.com/2014/04/roundtrip-serialization-of-datetimes-in.html"
			WriteByte((byte)BinarySerialisationDataType.DateTime);
			Int64WithoutDataType(value.Ticks);
			WriteByte((byte)value.Kind);
		}

		public void ArrayStart(object value, Type elementType)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (!(value is Array))
				throw new ArgumentException($"If {nameof(value)} is not null then it must be an array");
			if (elementType == null)
				throw new ArgumentNullException(nameof(elementType));

			WriteByte((byte)BinarySerialisationDataType.ArrayStart);
			WriteTypeName(elementType);
			Int32(((Array)value).Length);
		}

		public void ArrayEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ArrayEnd);
		}

		public void Null()
		{
			WriteByte((byte)BinarySerialisationDataType.Null);
		}

		public void ObjectStart(object value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			WriteByte((byte)BinarySerialisationDataType.ObjectStart);
			WriteTypeName(value.GetType());
		}

		public void ObjectEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectEnd);
		}

		public void ReferenceId(int value)
		{
			VariableLengthInt32(value, BinarySerialisationDataType.ReferenceID8, BinarySerialisationDataType.ReferenceID16, BinarySerialisationDataType.ReferenceID24, BinarySerialisationDataType.ReferenceID32);
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

		private static readonly int _int24Min = -(int)Math.Pow(2, 23);
		private static readonly int _int24Max = (int)(Math.Pow(2, 23) - 1);
		internal void VariableLengthInt32(int value, BinarySerialisationDataType int8, BinarySerialisationDataType int16, BinarySerialisationDataType int24, BinarySerialisationDataType int32)
		{
			if ((value >= byte.MinValue) && (value <= byte.MaxValue))
				WriteBytes((byte)int8, (byte)value);
			else if ((value >= short.MinValue) && (value <= short.MaxValue))
				WriteBytes((new Int16Bytes((short)value)).GetLittleEndianBytesWithDataType(int16));
			else if ((value >= _int24Min) && (value <= _int24Max))
				WriteBytes((new Int24Bytes(value)).GetLittleEndianBytesWithDataType(int24));
			else
				WriteBytes((new Int32Bytes(value)).GetLittleEndianBytesWithDataType(int32));
		}

		private void Int64WithoutDataType(long value)
		{
			WriteBytes(
				(byte)(value >> 56),
				(byte)(value >> 48),
				(byte)(value >> 40),
				(byte)(value >> 32),
				(byte)(value >> 24),
				(byte)(value >> 16),
				(byte)(value >> 8),
				(byte)value
			);
		}

		private void Int16WithoutDataType(short value)
		{
			WriteBytes((new Int16Bytes(value)).GetLittleEndianBytesWithoutDataType());
		}

		private void Int24WithoutDataType(int value)
		{
			WriteBytes((new Int24Bytes(value)).GetLittleEndianBytesWithoutDataType());
		}

		private void Int32WithoutDataType(int value)
		{
			WriteBytes((new Int32Bytes(value)).GetLittleEndianBytesWithoutDataType());
		}

		private void StringWithoutDataType(string value)
		{
			if (value == null)
			{
				Int32(-1);
				return;
			}

			var bytes = Encoding.UTF8.GetBytes(value);
			Int32(bytes.Length);
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

		// These WriteBytes overloads that take multiple individual bytes are to make it easier to compare calling WriteByte multiple times and wrapping into an array to pass to WriteBytes once
		// (2018-06-30: BenchmarkDotNet seems to report that it's slightly WORSE to call WriteBytes with an array but the flame graph in Code Track indicates a significant speed increase if a
		// single call to WriteByte is used.. I'd like the Benchmark stats to get better but it's only a small decrease shown there and a big increase elsewhere and so I'm going with my gut)
		private void WriteBytes(byte b0, byte b1)
		{
			WriteBytes(new[] { b0, b1 });
		}
		private void WriteBytes(byte b0, byte b1, byte b2)
		{
			WriteBytes(new[] { b0, b1, b2 });
		}
		private void WriteBytes(byte b0, byte b1, byte b2, byte b3)
		{
			WriteBytes(new[] { b0, b1, b2, b3 });
		}
		private void WriteBytes(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7)
		{
			WriteBytes(new[] { b0, b1, b2, b3, b4, b5, b6, b7 });
		}
		private void WriteBytes(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7, byte b8)
		{
			WriteBytes(new[] { b0, b1, b2, b3, b4, b5, b6, b7, b8 });
		}
	}
}