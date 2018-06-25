using System;
using System.Reflection;

namespace DanSerialiser
{
	internal static class BinaryReaderWriterShared
	{

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

		public static string CombineTypeAndFieldName(string typeNameIfRequired, string fieldName)
		{
			if (fieldName == null)
				throw new ArgumentNullException(nameof(fieldName));

			return ((typeNameIfRequired == null) ? "" : (typeNameIfRequired + "\n")) + fieldName;
		}

		public static void SplitCombinedTypeAndFieldName(string value, out string typeNameIfRequired, out string fieldName)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			var splitAt = value.IndexOf('\n');
			if (splitAt == -1)
			{
				typeNameIfRequired = null;
				fieldName = value;
				return;
			}
			typeNameIfRequired = value.Substring(0, splitAt);
			fieldName = value.Substring(splitAt + 1);
		}
	}
}