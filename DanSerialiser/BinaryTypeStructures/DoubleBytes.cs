using System;
using System.Runtime.InteropServices;

namespace DanSerialiser.BinaryTypeStructures
{
	/// <summary>
	/// Ingenious way to avoid having to call BitConverter.GetBytes for a double by relying on the fact that .NET represents a double as eight consecutive bytes in memory and
	/// that a struct with an explicit arrangement of fields can expose this data if there is a double field with offset zero AND byte fields that go from offsets zero to
	/// seven. Alas, I did not concoct this idea myself, I found it while looking through the source code for MessagePack, specifically this file:
	/// 
	///   https://github.com/msgpack/msgpack-cli/blob/5fb2430b7e05c5958d23a096c800107fe35ba48e/src/MsgPack/Float64Bits.cs
	/// 
	/// .. which is licensed under the Apache License Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0) and "Copyright (C) 2010-2016 FUJIWARA, Yusuke"
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct DoubleBytes
	{
		[FieldOffset(0)]
		public readonly double Value;

		[FieldOffset(0)]
		public readonly Byte Byte0;

		[FieldOffset(1)]
		public readonly Byte Byte1;

		[FieldOffset(2)]
		public readonly Byte Byte2;

		[FieldOffset(3)]
		public readonly Byte Byte3;

		[FieldOffset(4)]
		public readonly Byte Byte4;

		[FieldOffset(5)]
		public readonly Byte Byte5;

		[FieldOffset(6)]
		public readonly Byte Byte6;

		[FieldOffset(7)]
		public readonly Byte Byte7;

		public DoubleBytes(double value)
		{
			this = default(DoubleBytes); // Have to do this to avoid "Field 'Byte{x}' must be fully assigned before control is returned to the caller" errors
			this.Value = value;
		}

		public DoubleBytes(byte[] littleEndianBytes)
		{
			if (littleEndianBytes == null)
				throw new ArgumentNullException(nameof(littleEndianBytes));
			if (littleEndianBytes.Length != 8)
				throw new ArgumentException($"There must be precisely eight bytes in the {nameof(littleEndianBytes)} bytes array");

			this = default(DoubleBytes); // Have to do this to avoid "Field 'Value' must be fully assigned before control is returned to the caller" error
			if (BitConverter.IsLittleEndian)
			{
				this.Byte0 = littleEndianBytes[0];
				this.Byte1 = littleEndianBytes[1];
				this.Byte2 = littleEndianBytes[2];
				this.Byte3 = littleEndianBytes[3];
				this.Byte4 = littleEndianBytes[4];
				this.Byte5 = littleEndianBytes[4];
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
	}
}