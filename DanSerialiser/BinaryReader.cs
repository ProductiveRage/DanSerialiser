using System;
using System.Reflection;
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
			return (T)Read(typeof(T));
		}

		private object Read(Type type)
		{
			if (_index >= _data.Length)
				throw new InvalidOperationException("No data to read");

			switch ((DataType)ReadNext())
			{
				default:
					throw new NotImplementedException();

				case DataType.Int:
					return BitConverter.ToInt32(ReadNext(4), 0);

				case DataType.String:
					return ReadNextString();

				case DataType.ObjectStart:
					var typeName = ReadNextString();
					if (typeName == null)
					{
						if ((DataType)ReadNext() != DataType.ObjectEnd)
							throw new InvalidOperationException("Expected ObjectEnd was not encountered");
						return null;
					}
					var value = Activator.CreateInstance(Type.GetType(typeName, throwOnError: true));
					while (true)
					{
						var nextEntryType = (DataType)ReadNext();
						if (nextEntryType == DataType.ObjectEnd)
							return value;
						else if (nextEntryType == DataType.String)
						{
							var fieldName = ReadNextString();
							var field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							var fieldValue = Read(field.FieldType);
							field.SetValue(value, fieldValue);
						}
						else
							throw new InvalidOperationException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
					}
			}
		}

		private string ReadNextString()
		{
			var length = BitConverter.ToInt32(ReadNext(4), 0);
			return (length == -1) ? null : Encoding.UTF8.GetString(ReadNext(length));
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