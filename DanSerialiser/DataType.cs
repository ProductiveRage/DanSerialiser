namespace DanSerialiser
{
	internal enum DataType : byte
	{
		Byte,
		Int,

		String,

		ObjectStart,
		FieldName,
		ObjectEnd,

		ListStart,
		ListEnd
	}
}