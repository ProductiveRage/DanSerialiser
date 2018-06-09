using System;
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
			if (_index >= _data.Length)
				throw new InvalidOperationException("No data to read");

			switch ((DataType)ReadNext())
			{
				default:
					throw new NotImplementedException();

				case DataType.Int:
					return (T)(object)BitConverter.ToInt32(ReadNext(4), 0);

				case DataType.String:
					return (T)(object)ReadNextString();

				case DataType.ObjectStart:
					var typeName = ReadNextString();
					T value;
					if (typeName == null)
						value = default(T);
					else
					{
						var type = Type.GetType(typeName, throwOnError: true);
						value = (T)Activator.CreateInstance(type);
					}
					if ((DataType)ReadNext() != DataType.ObjectEnd)
						throw new InvalidOperationException("Expected ObjectEnd was not encountered");
					return value;
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