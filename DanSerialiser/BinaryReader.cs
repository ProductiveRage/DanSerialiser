using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace DanSerialiser
{
	public sealed class BinaryReader
	{
		private byte[] _data;
		private int _index;
		public BinaryReader(byte[] data)
		{
			_data = data ?? throw new ArgumentNullException(nameof(data));
			_index = 0;
		}

		public T Read<T>()
		{
			// The original intention of the use of generic type params for the reader and writer was to reduce casting at the call sites and I had thought that the type might
			// be required when deserialising - but it is not for the current BinaryWriter and BinaryReader implementations, so all we do with T here is try to cast the return
			// value to it
			return (T)Read();
		}

		private object Read()
		{
			if (_index >= _data.Length)
				throw new InvalidOperationException("No data to read");

			switch ((DataType)ReadNext())
			{
				default:
					throw new NotImplementedException();

				case DataType.Boolean:
					return ReadNext() != 0;
				case DataType.Byte:
					return ReadNext();
				case DataType.SByte:
					return (sbyte)ReadNext();

				case DataType.Int16:
					return BitConverter.ToInt16(ReadNext(sizeof(Int16)), 0);
				case DataType.Int32:
					return ReadNextInt();
				case DataType.Int64:
					return BitConverter.ToInt64(ReadNext(sizeof(Int64)), 0);

				case DataType.UInt16:
					return BitConverter.ToUInt16(ReadNext(sizeof(UInt16)), 0);
				case DataType.UInt32:
					return BitConverter.ToUInt32(ReadNext(sizeof(UInt32)), 0);
				case DataType.UInt64:
					return BitConverter.ToUInt64(ReadNext(sizeof(UInt64)), 0);

				case DataType.Single:
					return BitConverter.ToSingle(ReadNext(sizeof(Single)), 0);
				case DataType.Double:
					return BitConverter.ToDouble(ReadNext(sizeof(Double)), 0);
				case DataType.Decimal:
					// BitConverter does not deal with decimal (there is no GetBytes overloads for it and no ToDecimal method) so BinaryWriter used decimal.GetBits, which
					// returns four int values and so we need to do the opposite here
					var partialValues = new int[4];
					for (var i = 0; i < 4; i++)
						partialValues[i] = ReadNextInt();
					return new decimal(partialValues);

				case DataType.Char:
					return BitConverter.ToChar(ReadNext(sizeof(Char)), 0);
				case DataType.String:
					return ReadNextString();

				case DataType.ListStart:
					return ReadNextList();

				case DataType.ObjectStart:
					return ReadNextObject();
			}
		}

		private int ReadNextInt()
		{
			return BitConverter.ToInt32(ReadNext(sizeof(Int32)), 0);
		}

		private string ReadNextString()
		{
			var length = ReadNextInt();
			return (length == -1) ? null : Encoding.UTF8.GetString(ReadNext(length));
		}

		private object ReadNextObject()
		{
			var typeName = ReadNextString();
			if (typeName == null)
			{
				if ((DataType)ReadNext() != DataType.ObjectEnd)
					throw new InvalidOperationException("Expected ObjectEnd was not encountered");
				return null;
			}
			var value = FormatterServices.GetUninitializedObject(Type.GetType(typeName, throwOnError: true));
			while (true)
			{
				var nextEntryType = (DataType)ReadNext();
				if (nextEntryType == DataType.ObjectEnd)
					return value;
				else if (nextEntryType == DataType.FieldName)
				{
					var fieldOrTypeName = ReadNextString();
					string typeNameIfRequired, fieldName;
					if (fieldOrTypeName.StartsWith(BinaryWriter.FieldTypeNamePrefix))
					{
						typeNameIfRequired = fieldOrTypeName.Substring(BinaryWriter.FieldTypeNamePrefix.Length);
						fieldName = ReadNextString();
					}
					else
					{
						typeNameIfRequired = null;
						fieldName = fieldOrTypeName;
					}
					var typeToLookForMemberOn = value.GetType();
					FieldInfo field;
					while (true)
					{
						field = typeToLookForMemberOn.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if ((field != null) && ((typeNameIfRequired == null) || (field.DeclaringType.AssemblyQualifiedName == typeNameIfRequired)))
							break;
						typeToLookForMemberOn = typeToLookForMemberOn.BaseType;
						if (typeToLookForMemberOn == null)
							break;
					}
					var fieldValue = Read();
					if (field == null)
					{
						// If the serialised data has content for a field that does not exist on the target type then don't try to set it - this may happen if the version of the
						// assembly that was used when it was serialised is different to the version loaded when deserialising. Being flexible about this means that is a newer
						// version has an additional property added to it then older code can still read it.
						continue;
					}
					field.SetValue(value, fieldValue);
				}
				else
					throw new InvalidOperationException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
			}
		}

		private object ReadNextList()
		{
			var typeName = ReadNextString();
			if (typeName == null)
			{
				if ((DataType)ReadNext() != DataType.ListEnd)
					throw new InvalidOperationException("Expected ListEnd was not encountered");
				return null;
			}
			var type = Type.GetType(typeName, throwOnError: true);
			var elementType = type.GetElementType();
			if (elementType == null)
			{
				elementType = type.TryToGetIEnumerableElementType();
				if (elementType == null)
					throw new InvalidOperationException("Unable to determine element type from list type: " + type.Name);
			}
			var items = Array.CreateInstance(elementType, length: ReadNextInt());
			for (var i = 0; i < items.Length; i++)
				items.SetValue(Read(), i);
			var nextEntryType = (DataType)ReadNext();
			if (nextEntryType != DataType.ListEnd)
				throw new InvalidOperationException("Expected ListEnd was not encountered");
			if (type.IsArray)
				return items;
			var constructor =
				type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(IEnumerable<>).MakeGenericType(elementType) }, null) ??
				type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(IEnumerable) }, null);
			if (constructor == null)
				throw new InvalidOperationException("Unable to identify constructor for list type: " + type.Name);
			return constructor.Invoke(new[] { items });
		}

		private byte ReadNext()
		{
			return ReadNext(1)[0];
		}

		private byte[] ReadNext(int numberOfBytes)
		{
			if (_index + numberOfBytes > _data.Length)
				throw new InvalidOperationException("Insufficient data to read (presume invalid content)");

			var values = new byte[numberOfBytes];
			Array.Copy(_data, _index, values, 0, numberOfBytes);
			_index += numberOfBytes;
			return values;
		}
	}
}