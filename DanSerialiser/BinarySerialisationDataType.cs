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

		Null,

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
		ObjectContentPostponed,
		ObjectEnd,

		ArrayStart,
		ArrayEnd,

		// 2018-09-26 DWR: New values should go at the end of the enum list so that existing enum values don't change (eg. if TimeSpan was added under
		// DateTime, to try to group the values together, then everything after it would get a underlying new byte value - this could be worked around
		// by explicitly setting each value but that feels error prone and so I'll do it this way instead)
		TimeSpan,
		Guid
	}
}