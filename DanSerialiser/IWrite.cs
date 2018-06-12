using System;
using System.Reflection;

namespace DanSerialiser
{
	public interface IWrite
	{
		void Boolean(bool value);
		void Byte(byte value);
		void SByte(sbyte value);

		void Int16(short value);
		void Int32(int value);
		void Int64(long value);

		void UInt16(ushort value);
		void UInt32(uint value);
		void UInt64(ulong value);

		void Single(float value);
		void Double(double value);
		void Decimal(decimal value);

		void Char(char value);
		void String(string value);

		void ArrayStart(object value, Type elementType);
		void ArrayEnd();

		void ObjectStart<T>(T value);
		void ObjectEnd();

		/// <summary>
		/// This will return false if the field should be skipped
		/// </summary>
		bool FieldName(FieldInfo field, Type serialisationTargetType);
	}
}