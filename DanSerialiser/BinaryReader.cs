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
					var length = BitConverter.ToInt32(ReadNext(4), 0);
					var value = (length == -1) ? null : Encoding.UTF8.GetString(ReadNext(length));
					return (T)(object)value;
			}

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