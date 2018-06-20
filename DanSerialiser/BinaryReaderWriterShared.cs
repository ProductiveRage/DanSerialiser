using System;
using System.Reflection;

namespace DanSerialiser
{
	internal static class BinaryReaderWriterShared
	{
		public const string FieldTypeNamePrefix = "#type#";

		public static readonly BindingFlags MemberRetrievalBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static readonly int MaxReferenceCount = int.MaxValue;

		public static bool IgnoreField(FieldInfo field)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));

			if (field.GetCustomAttribute<NonSerializedAttribute>() != null)
				return true;

			// If the pointer is a field on an Exception then ignore the field - the rest of the exception information should make it through and be useful but there
			// should be an understanding that not everything within an exception can be captured when serialised
			if (field.DeclaringType == typeof(Exception))
			{
				if (field.FieldType.IsPointer || (field.FieldType == typeof(IntPtr)) || (field.FieldType == typeof(UIntPtr)))
					return true;
			}

			return false;
		}
	}
}