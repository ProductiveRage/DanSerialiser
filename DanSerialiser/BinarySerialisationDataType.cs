namespace DanSerialiser
{
	internal enum BinarySerialisationDataType : byte
	{
		Boolean = 0,
		Byte = 1,
		SByte = 2,

		Int16 = 3,
		Int32_8 = 4,
		Int32_16 = 5,
		Int32_24 = 6,
		Int32 = 7,
		Int64 = 8,

		UInt16 = 9,
		UInt32 = 10,
		UInt64 = 11,

		Single = 12,
		Double = 13,
		Decimal = 14,

		Char = 15,
		String = 16,

		DateTime = 17,
		TimeSpan = 36,

		Guid = 37,

		Null = 18,

		ObjectStart = 19,
		NameReferenceID8 = 20,
		NameReferenceID16 = 21,
		NameReferenceID24 = 22,
		NameReferenceID32 = 23,
		ReferenceID8 = 24,
		ReferenceID16 = 25,
		ReferenceID24 = 26,
		ReferenceID32 = 27,
		FieldName = 28,
		ObjectContentPostponed = 29,
		ObjectEnd = 30,
		ArrayStart = 31,
		ArrayEnd = 32
	}
}