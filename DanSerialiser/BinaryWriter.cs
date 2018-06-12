using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DanSerialiser
{
	public sealed class BinaryWriter : IWrite
	{
		private readonly List<byte> _data;
		public BinaryWriter()
		{
			_data = new List<byte>();
		}

		public void Boolean(bool value)
		{
			_data.Add((byte)DataType.Boolean);
			_data.Add(value ? (byte)1 : (byte)0);
		}
		public void Byte(byte value)
		{
			_data.Add((byte)DataType.Byte);
			_data.Add(value);
		}
		public void SByte(sbyte value)
		{
			_data.Add((byte)DataType.SByte);
			_data.Add((byte)value);
		}

		public void Int16(short value)
		{
			_data.Add((byte)DataType.Int16);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void Int32(int value)
		{
			_data.Add((byte)DataType.Int32);
			IntWithoutDataType(value);
		}
		public void Int64(long value)
		{
			_data.Add((byte)DataType.Int64);
			_data.AddRange(BitConverter.GetBytes(value));
		}

		public void Single(float value)
		{
			_data.Add((byte)DataType.Single);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void Double(double value)
		{
			_data.Add((byte)DataType.Double);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void Decimal(decimal value)
		{
			// BitConverter's "GetBytes" method doesn't support decimal so use "decimal.GetBits" that returns four int values
			_data.Add((byte)DataType.Decimal);
			foreach (var partialValue in decimal.GetBits(value))
				IntWithoutDataType(partialValue);
		}

		public void UInt16(ushort value)
		{
			_data.Add((byte)DataType.UInt16);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void UInt32(uint value)
		{
			_data.Add((byte)DataType.UInt32);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void UInt64(ulong value)
		{
			_data.Add((byte)DataType.UInt64);
			_data.AddRange(BitConverter.GetBytes(value));
		}

		public void Char(char value)
		{
			_data.Add((byte)DataType.Char);
			_data.AddRange(BitConverter.GetBytes(value));
		}
		public void String(string value)
		{
			_data.Add((byte)DataType.String);
			StringWithoutDataType(value);
		}

		public void ArrayStart(object value, Type elementType)
		{
			if (elementType == null)
				throw new ArgumentNullException(nameof(elementType));
			if ((value != null) && !(value is Array))
				throw new ArgumentException($"If {nameof(value)} is not null then it must be an array");

			_data.Add((byte)DataType.ArrayStart);
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
			_data.Add((byte)DataType.ArrayEnd);
		}

		public void ObjectStart<T>(T value)
		{
			_data.Add((byte)DataType.ObjectStart);
			StringWithoutDataType(value?.GetType()?.AssemblyQualifiedName);
		}

		public void ObjectEnd()
		{
			_data.Add((byte)DataType.ObjectEnd);
		}

		public bool FieldName(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			if (field.GetCustomAttribute<NonSerializedAttribute>() != null)
				return false;

			// If a field is declared multiple times in the type hierarchy (whether through overrides or use of "new") then its name will need prefixing with the type
			// that this FieldInfo relates to
			var fieldNameExistsMultipleTimesInHierarchy = false;
			var currentType = serialisationTargetType;
			while (currentType != null)
			{
				if (currentType != serialisationTargetType)
				{
					if (currentType.GetFields(BinaryReaderWriterConstants.FieldRetrievalBindingFlags).Any(f => f.Name == field.Name))
					{
						fieldNameExistsMultipleTimesInHierarchy = true;
						break;
					}
				}
				currentType = currentType.BaseType;
			}
			_data.Add((byte)DataType.FieldName);
			if (fieldNameExistsMultipleTimesInHierarchy)
				StringWithoutDataType(BinaryReaderWriterConstants.FieldTypeNamePrefix + field.DeclaringType.AssemblyQualifiedName);
			StringWithoutDataType(field.Name);
			return true;
		}

		public byte[] GetData()
		{
			return _data.ToArray();
		}

		private void IntWithoutDataType(int value)
		{
			_data.AddRange(BitConverter.GetBytes(value));
		}

		private void StringWithoutDataType(string value)
		{
			if (value == null)
			{
				_data.AddRange(BitConverter.GetBytes(-1));
				return;
			}
			var bytes = Encoding.UTF8.GetBytes(value);
			_data.AddRange(BitConverter.GetBytes(bytes.Length));
			_data.AddRange(bytes);
		}
	}
}