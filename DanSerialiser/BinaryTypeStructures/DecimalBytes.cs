using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct DecimalBytes
	{
		public const int BytesRequired = 16;

		[FieldOffset(0)]
		public readonly decimal Value;

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

		[FieldOffset(8)]
		private readonly Byte Byte8;

		[FieldOffset(9)]
		private readonly Byte Byte9;

		[FieldOffset(10)]
		private readonly Byte Byte10;

		[FieldOffset(11)]
		private readonly Byte Byte11;

		[FieldOffset(12)]
		private readonly Byte Byte12;

		[FieldOffset(13)]
		private readonly Byte Byte13;

		[FieldOffset(14)]
		private readonly Byte Byte14;

		[FieldOffset(15)]
		private readonly Byte Byte15;

		public DecimalBytes(decimal value)
		{
			this = default(DecimalBytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public DecimalBytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != BytesRequired)
				throw new ArgumentException($"There must be precisely {BytesRequired} bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(DecimalBytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
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
				this.Byte8 = littleEndianBytes[8];
				this.Byte9 = littleEndianBytes[9];
				this.Byte10 = littleEndianBytes[10];
				this.Byte11 = littleEndianBytes[11];
				this.Byte12 = littleEndianBytes[12];
				this.Byte13 = littleEndianBytes[13];
				this.Byte14 = littleEndianBytes[14];
				this.Byte15 = littleEndianBytes[15];
			}
			else
			{
				this.Byte0 = littleEndianBytes[15];
				this.Byte1 = littleEndianBytes[14];
				this.Byte2 = littleEndianBytes[13];
				this.Byte3 = littleEndianBytes[12];
				this.Byte4 = littleEndianBytes[11];
				this.Byte5 = littleEndianBytes[10];
				this.Byte6 = littleEndianBytes[9];
				this.Byte7 = littleEndianBytes[8];
				this.Byte8 = littleEndianBytes[7];
				this.Byte9 = littleEndianBytes[6];
				this.Byte10 = littleEndianBytes[5];
				this.Byte11 = littleEndianBytes[4];
				this.Byte12 = littleEndianBytes[3];
				this.Byte13 = littleEndianBytes[2];
				this.Byte14 = littleEndianBytes[1];
				this.Byte15 = littleEndianBytes[0];
			}
		}

		public byte[] GetLittleEndianBytesWithDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { (byte)BinarySerialisationDataType.Decimal, Byte0, Byte1, Byte2, Byte3, Byte4, Byte5, Byte6, Byte7, Byte8, Byte9, Byte10, Byte11, Byte12, Byte13, Byte14, Byte15 };
			else
				return new[] { (byte)BinarySerialisationDataType.Decimal, Byte15, Byte14, Byte13, Byte12, Byte11, Byte10, Byte9, Byte8, Byte7, Byte6, Byte5, Byte4, Byte3, Byte2, Byte1, Byte0 };
		}
	}
}