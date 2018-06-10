namespace DanSerialiser
{
	internal enum DataType : byte
	{
		Boolean,
		Byte,
		SByte,

		Int16,
		Int32,
		Int64,

		UInt16,
		UInt32,
		UInt64,

		Single,
		Double,
		Decimal,

		Char,
		String,

		ObjectStart,
		FieldName,
		ObjectEnd,

		ListStart,
		ListEnd
	}
}