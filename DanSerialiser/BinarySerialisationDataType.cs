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
		/// <summary>
		/// This is a field name data for a type that will appear later - ordinarily, the first time that a field is present in the content, the field name will be included along with
		/// a Name Reference ID (and then subsequent appearances of that field will specify only the Name Reference ID) but sometimes it may be desirable to list all of the field names
		/// first and then only use Name Reference IDs within the object data. If this is the case then the FieldNamePreLoad entries will always be at the start of the content and they
		/// may not appear again once the object data begins.
		/// </summary>
		FieldNamePreLoad = 33,
		/// <summary>
		/// Same principle as FieldNamePreLoad - declare type names upfront and then always includes the Name Reference IDs in the content, rather than having to check a has-type-name-
		/// been-included-in-serialised-data yet each time (as with FieldNamePreLoad, whether or not TypeNamePreLoad data appears will vary depending by writer configuration)
		/// </summary>
		TypeNamePreLoad = 34,
		/// <summary>
		/// This indicates that the reference should be instantiated but left unitialised for now and that the member data will appear later (against the same Reference ID)
		/// </summary>
		ObjectContentPostponed = 29,
		ObjectEnd = 30,
		ArrayStart = 31,
		ArrayEnd = 32
	}
}