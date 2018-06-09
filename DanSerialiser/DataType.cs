namespace DanSerialiser
{
	internal enum DataType : byte
	{
		Int,

		String,

		ObjectStart,
		FieldName,
		ObjectEnd
	}
}