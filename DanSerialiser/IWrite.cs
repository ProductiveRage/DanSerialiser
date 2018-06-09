namespace DanSerialiser
{
	public interface IWrite
	{
		void Int32(int value);
		void String(string value);
		void ObjectStart<T>(T value);
		void ObjectEnd();
	}
}