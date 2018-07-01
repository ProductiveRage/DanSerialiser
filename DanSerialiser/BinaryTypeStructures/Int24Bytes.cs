using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct Int24Bytes
	{
		[FieldOffset(0)]
		public readonly int Value;

		[FieldOffset(0)]
		private readonly Byte Byte0;

		[FieldOffset(1)]
		private readonly Byte Byte1;

		[FieldOffset(2)]
		private readonly Byte Byte2;

		[FieldOffset(3)]
		private readonly Byte Byte3;

		public Int24Bytes(int value)
		{
			this = default(Int24Bytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public Int24Bytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != 3)
				throw new ArgumentException($"There must be precisely three bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(Int24Bytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
			if (BitConverter.IsLittleEndian)
			{
				this.Byte0 = littleEndianBytes[0];
				this.Byte1 = littleEndianBytes[1];
				this.Byte2 = littleEndianBytes[2];
			}
			else
			{
				this.Byte0 = littleEndianBytes[2];
				this.Byte1 = littleEndianBytes[1];
				this.Byte2 = littleEndianBytes[0];
			}
			var isNegative = (this.Byte2 & 128) != 0;
			this.Byte3 = isNegative ? (byte)255 : (byte)0;
		}

		public byte[] GetLittleEndianBytesWithDataType(BinarySerialisationDataType int24)
		{
			if (BitConverter.IsLittleEndian)
				return new[] { (byte)int24, Byte0, Byte1, Byte2};
			else
				return new[] { (byte)int24, Byte2, Byte1, Byte0 };
		}
		public byte[] GetLittleEndianBytesWithoutDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { Byte0, Byte1, Byte2 };
			else
				return new[] { Byte2, Byte1, Byte0 };
		}
	}
}