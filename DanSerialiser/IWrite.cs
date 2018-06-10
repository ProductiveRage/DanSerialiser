namespace DanSerialiser
{
	public interface IWrite
	{
		void Byte(byte value);
		void Int32(int value);
		void String(string value);
		void ListStart<T>(T value);
		void ListEnd();
		void ObjectStart<T>(T value);
		void FieldName(string name, string typeNameIfRequired);
		void ObjectEnd();
	}
}