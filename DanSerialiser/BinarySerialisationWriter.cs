using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DanSerialiser
{
	public sealed class BinarySerialisationWriter : IWrite
	{
		private readonly Stream _stream;
		public BinarySerialisationWriter(Stream stream, bool supportReferenceReuse = false)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			SupportReferenceReuse = supportReferenceReuse;
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
			if (value == null)
			{
				// If the value is null then don't store the element type since we don't need it (and the BinaryReader will understand that this represents a null value)
				StringWithoutDataType(null);
				return;
			}
			StringWithoutDataType(elementType.AssemblyQualifiedName);
			IntWithoutDataType(((Array)(object)value).Length);
		}

		public void ArrayEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ArrayEnd);
		}

		public void ObjectStart<T>(T value)
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectStart);
			StringWithoutDataType(value?.GetType()?.AssemblyQualifiedName);
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

			if (BinaryReaderWriterShared.IgnoreField(field))
				return false;

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
			WriteByte((byte)BinarySerialisationDataType.FieldName);
			if (fieldNameExistsMultipleTimesInHierarchy)
				StringWithoutDataType(BinaryReaderWriterShared.FieldTypeNamePrefix + field.DeclaringType.AssemblyQualifiedName);
			StringWithoutDataType(field.Name);
			return true;
		}

		public bool PropertyName(PropertyInfo property, Type serialisationTargetType)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			// Most of the time, we'll just serialise the backing fields because that should capture all of the data..
			if (property.GetCustomAttribute<DeprecatedAttribute>() == null)
				return false;

			// .. however, if this is a property that has the [Deprecated] attribute on it then it is expected to exist for backwards compatibility and to be a computed property
			// (and so have no backing field) but one that we want to include in the serialised data anyway. If V1 of a type has a string "Name" property which is replaced in V2
			// with a "TranslatedName" property of type TranslatedString then a computed "Name" property could be added to the V2 type (annotated with [Deprecated]) whose getter
			// returns the default language value of the TranslatedName - this value may then be included in the serialisation data so that an assembly that has loaded the V1
			// type can deserialise and populate its Name property.
			// - Note: We won't try to determine whether or not the type name prefix is necessary when recording the field name because the type hierarchy and the properties on
			//   them might be different now than in the version of the types where deserialisation occurs so the type name will always be inserted before the field name to err
			//   on the safe side
			WriteByte((byte)BinarySerialisationDataType.FieldName);
			StringWithoutDataType(BinaryReaderWriterShared.FieldTypeNamePrefix + property.DeclaringType.AssemblyQualifiedName);
			StringWithoutDataType(BackingFieldHelpers.GetBackingFieldName(property.Name));
			return true;
		}

		private void IntWithoutDataType(int value)
		{
			WriteBytes(BitConverter.GetBytes(value));
		}

		private void StringWithoutDataType(string value)
		{
			if (value == null)
			{
				WriteBytes(BitConverter.GetBytes(-1));
				return;
			}
			var bytes = Encoding.UTF8.GetBytes(value);
			WriteBytes(BitConverter.GetBytes(bytes.Length));
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