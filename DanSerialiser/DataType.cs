namespace DanSerialiser
{
	internal enum DataType : byte
	{
		Byte,
		Int16,
		Int32,
		Int64,

		String,

		ObjectStart,
		FieldName,
		ObjectEnd,

		ListStart,
		ListEnd
	}
}