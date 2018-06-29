namespace DanSerialiser
{
	internal enum BinarySerialisationDataType : byte
	{
		Boolean,
		Byte,
		SByte,

		Int16,
		Int32,
		Int32_Byte,
		Int32_Int16,
		Int64,

		UInt16,
		UInt32,
		UInt64,

		Single,
		Double,
		Decimal,

		Char,
		String,

		DateTime,

		ObjectStart,
		NameReferenceID8,
		NameReferenceID16,
		NameReferenceID32,
		ReferenceID,
		FieldName,
		ObjectEnd,

		ArrayStart,
		ArrayEnd
	}
}