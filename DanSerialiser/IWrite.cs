using System;
using System.Reflection;

namespace DanSerialiser
{
	public interface IWrite
	{
		ReferenceReuseOptions ReferenceReuseStrategy { get; }

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

		void DateTime(DateTime value);
		void TimeSpan(TimeSpan value);

		void Guid(Guid value);

		void ArrayStart(object value, Type elementType);
		void ArrayEnd();

		void Null();

		void ObjectStart(object value);
		void ObjectEnd();
		void ReferenceId(int value);

		/// <summary>
		/// This indicates that the current object reference being serialised will have its member data written later - when deserialising, an uninitialised instance should be created
		/// that will later have its fields and properties set
		/// </summary>
		void ObjectContentPostponed();

		/// <summary>
		/// This should only be called when writing out data for deferred-initialised object references - otherwise the boolean return value from the FieldName method will indicate
		/// whether a field should be serialised or not
		/// </summary>
		bool ShouldSerialiseField(FieldInfo field, Type serialisationTargetType);

		/// <summary>
		/// This should only be called when writing out data for deferred-initialised object references - otherwise the boolean return value from the FieldName method will indicate
		/// whether a property should be serialised or not
		/// </summary>
		bool ShouldSerialiseProperty(PropertyInfo property, Type serialisationTargetType);

		/// <summary>
		/// This will return false if the field should be skipped
		/// </summary>
		bool FieldName(FieldInfo field, Type serialisationTargetType);
		
		/// <summary>
		/// This will return false if the property should be skipped
		/// </summary>
		bool PropertyName(PropertyInfo property, Type serialisationTargetType);

		/// <summary>
		/// This will return a compiled 'member setter' for the specified type, if it's possible to create one. A member setter takes an instance of an object and writes the data
		/// for the fields and properties to the writer. It does not write the ObjectStart and ObjectEnd data since the caller takes responsibility for those because reference
		/// tracking is handled by the caller and it may need to inject a ReferenceID after the ObjectStart data. When reference tracking is enabled, only limited types may have
		/// a member setter generated for them because reference tracking is not possible for the field and property values that the member setter writes (and so the only types
		/// that member setters may be provided for will have fields and properties that are all primitive-like values, such as	genuine primitives and strings and DateTime and
		/// the other types that IWrite handles and that can never result in circular references). This will return null if a member setter could not be provided for the type.
		/// </summary>
		Action<object> TryToGenerateMemberSetter(Type type);
	}
}