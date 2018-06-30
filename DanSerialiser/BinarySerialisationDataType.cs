namespace DanSerialiser
{
	internal enum BinarySerialisationDataType : byte
	{
		Boolean,
		Byte,
		SByte,

		Int16,
		Int32_8,
		Int32_16,
		Int32_24,
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

		DateTime,

		ObjectStart,
		NameReferenceID8,
		NameReferenceID16,
		NameReferenceID24,
		NameReferenceID32,
		ReferenceID8,
		ReferenceID16,
		ReferenceID24,
		ReferenceID32,
		FieldName,
		ObjectEnd,

		ArrayStart,
		ArrayEnd
	}
}