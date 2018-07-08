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

		public static string CombineTypeAndFieldName(Type typeIfRequired, string fieldName)
		{
			if (fieldName == null)
				throw new ArgumentNullException(nameof(fieldName));

			// Note: If there is a type name specified then it will be a Type's "FullName" value and not its "AssemblyQualifiedName" because the AssemblyQualifiedName includes
			// the version number of the assembly and if there is a type name here then the chances are that this is to do with a [Deprecated] property, which means that the
			// versions of the assembly are expected to be different between now and when the serialised data was generated for some deserialisation attempts (when type
			// names appear elsewhere - for types to be instantiated - the AssemblyQualifiedName is required, though the Type.GetType method used to create instances
			// is conveniently flexible with version numbers - eg.
			//
			//   Type.GetType("Tester.PersonDetails, Tester, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null")
			//
			// will work even if the currently assembly version of Tester is a version other than 4.0.0.0 (TODO: So long as the type's assembly has already been loaded into
			// memory by that time - which is often the case, I hope, when the project has been built to reference the entities that will be de/serialised but which is possibly
			// something that could be improved.. it's something that I'm finding awkward to unit test at the moment, which I'm not happy about)
			return ((typeIfRequired == null) ? "" : (typeIfRequired.FullName + "\n")) + fieldName;
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