﻿using System;
using System.Collections.Generic;
using System.Text;

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

		public void String(string value)
		{
			_data.Add((byte)DataType.String);
			if (value == null)
			{
				_data.AddRange(BitConverter.GetBytes(-1));
				return;
			}
			var bytes = Encoding.UTF8.GetBytes(value);
			_data.AddRange(BitConverter.GetBytes(bytes.Length));
			_data.AddRange(bytes);
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