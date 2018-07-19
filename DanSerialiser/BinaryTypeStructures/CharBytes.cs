using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Similar approach as DoubleBytes
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct CharBytes
	{
		public const int BytesRequired = 2;

		[FieldOffset(0)]
		public readonly char Value;

		[FieldOffset(0)]
		private readonly Byte Byte0;

		[FieldOffset(1)]
		private readonly Byte Byte1;

		public CharBytes(char value)
		{
			this = default(CharBytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public CharBytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != 2)
				throw new ArgumentException($"There must be precisely two bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(CharBytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
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
				return new[] { (byte)BinarySerialisationDataType.Char, Byte0, Byte1 };
			else
				return new[] { (byte)BinarySerialisationDataType.Char, Byte1, Byte0 };
		}
	}
}