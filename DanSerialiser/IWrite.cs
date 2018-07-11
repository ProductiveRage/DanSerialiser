﻿using System;
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

		Action<object> TryToGenerateMemberSetter(Type type);
	}
}