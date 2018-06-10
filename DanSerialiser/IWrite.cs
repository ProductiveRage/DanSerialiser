namespace DanSerialiser
{
	public interface IWrite
	{
		void Boolean(bool value);
		void Byte(byte value);

		void Int16(short value);
		void Int32(int value);
		void Int64(long value);

		void UInt16(ushort value);
		void UInt32(uint value);
		void UInt64(ulong value);

		void Char(char value);
		void String(string value);

		void ListStart<T>(T value);
		void ListEnd();

		void ObjectStart<T>(T value);
		void FieldName(string name, string typeNameIfRequired);
		void ObjectEnd();
	}
}