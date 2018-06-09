using System;
using System.Collections.Generic;

namespace DanSerialiser
{
	public sealed class BinaryWriter : IWrite
	{
		private List<byte> _data;
		public BinaryWriter()
		{
			_data = new List<byte>();
		}

		public void Int32(int value)
		{
			_data.Add((byte)DataType.Int);
			_data.AddRange(BitConverter.GetBytes(value));
		}

		public byte[] GetData()
		{
			return _data.ToArray();
		}

		public void Dispose()
		{
			_data = null;
		}
	}
}