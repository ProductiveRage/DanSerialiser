using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct Int64Bytes
	{
		public const int BytesRequired = 8;

		[FieldOffset(0)]
		public readonly long Value;

		[FieldOffset(0)]
		private readonly Byte Byte0;

		[FieldOffset(1)]
		private readonly Byte Byte1;

		[FieldOffset(2)]
		private readonly Byte Byte2;

		[FieldOffset(3)]
		private readonly Byte Byte3;

		[FieldOffset(4)]
		private readonly Byte Byte4;

		[FieldOffset(5)]
		private readonly Byte Byte5;

		[FieldOffset(6)]
		private readonly Byte Byte6;

		[FieldOffset(7)]
		private readonly Byte Byte7;

		public Int64Bytes(long value)
		{
			this = default(Int64Bytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public Int64Bytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != 8)
				throw new ArgumentException($"There must be precisely eight bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(Int64Bytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
			if (BitConverter.IsLittleEndian)
			{
				this.Byte0 = littleEndianBytes[0];
				this.Byte1 = littleEndianBytes[1];
				this.Byte2 = littleEndianBytes[2];
				this.Byte3 = littleEndianBytes[3];
				this.Byte4 = littleEndianBytes[4];
				this.Byte5 = littleEndianBytes[5];
				this.Byte6 = littleEndianBytes[6];
				this.Byte7 = littleEndianBytes[7];
			}
			else
			{
				this.Byte0 = littleEndianBytes[7];
				this.Byte1 = littleEndianBytes[6];
				this.Byte2 = littleEndianBytes[5];
				this.Byte3 = littleEndianBytes[4];
				this.Byte4 = littleEndianBytes[3];
				this.Byte5 = littleEndianBytes[2];
				this.Byte6 = littleEndianBytes[1];
				this.Byte7 = littleEndianBytes[0];
			}
		}

		public byte[] GetLittleEndianBytesWithDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { (byte)BinarySerialisationDataType.Int64, Byte0, Byte1, Byte2, Byte3, Byte4, Byte5, Byte6, Byte7 };
			else
				return new[] { (byte)BinarySerialisationDataType.Int64, Byte7, Byte6, Byte5, Byte4, Byte3, Byte2, Byte1, Byte0 };
		}
		public byte[] GetLittleEndianBytesWithoutDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { Byte0, Byte1, Byte2, Byte3, Byte4, Byte5, Byte6, Byte7 };
			else
				return new[] { Byte7, Byte6, Byte5, Byte4, Byte3, Byte2, Byte1, Byte0 };
		}
	}
}