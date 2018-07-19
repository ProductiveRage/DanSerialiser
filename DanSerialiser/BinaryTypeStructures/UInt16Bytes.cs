using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct UInt16Bytes
	{
		public const int BytesRequired = 2;

		[FieldOffset(0)]
		public readonly ushort Value;

		[FieldOffset(0)]
		private readonly Byte Byte0;

		[FieldOffset(1)]
		private readonly Byte Byte1;

		public UInt16Bytes(ushort value)
		{
			this = default(UInt16Bytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public UInt16Bytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != 2)
				throw new ArgumentException($"There must be precisely two bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(UInt16Bytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
			if (BitConverter.IsLittleEndian)
			{
				this.Byte0 = littleEndianBytes[0];
				this.Byte1 = littleEndianBytes[1];
			}
			else
			{
				this.Byte0 = littleEndianBytes[1];
				this.Byte1 = littleEndianBytes[0];
			}
		}

		public byte[] GetLittleEndianBytesWithDataType()
		{
			if (BitConverter.IsLittleEndian)
				return new[] { (byte)BinarySerialisationDataType.UInt16, Byte0, Byte1};
			else
				return new[] { (byte)BinarySerialisationDataType.UInt16, Byte1, Byte0 };
		}
	}
}