using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct UInt32Bytes
	{
		public const int BytesRequired = 4;

		[FieldOffset(0)]
		public readonly uint Value;

		[FieldOffset(0)]
		private readonly Byte Byte0;

		[FieldOffset(1)]
		private readonly Byte Byte1;

		[FieldOffset(2)]
		private readonly Byte Byte2;

		[FieldOffset(3)]
		private readonly Byte Byte3;

		public UInt32Bytes(uint value)
		{
			this = default(UInt32Bytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public UInt32Bytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != BytesRequired)
				throw new ArgumentException($"There must be precisely {BytesRequired} bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(UInt32Bytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
			if (BitConverter.IsLittleEndian)
			{
				this.Byte0 = littleEndianBytes[0];
				this.Byte1 = littleEndianBytes[1];
				this.Byte2 = littleEndianBytes[2];
				this.Byte3 = littleEndianBytes[3];
			}
			else
			{
				this.Byte0 = littleEndianBytes[3];
				this.Byte1 = littleEndianBytes[2];
				this.Byte2 = littleEndianBytes[1];
				this.Byte3 = littleEndianBytes[0];
			}
		}

		public byte[] GetLittleEndianBytesWithDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { (byte)BinarySerialisationDataType.UInt32, Byte0, Byte1, Byte2, Byte3 };
			else
				return new[] { (byte)BinarySerialisationDataType.UInt32, Byte3, Byte2, Byte1, Byte0 };
		}
	}
}