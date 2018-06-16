using System;
using System.Reflection;

namespace DanSerialiser
{
	internal sealed class BackingFieldHelpers
	{
		private const string PREFIX = "<";
		private const string SUFFIX = ">k__BackingField";

		// Note: Don't want this method to try to find the backing field on the type for a given PropertyInfo because this may be used to serialise [Deprecated] properties that don't have backing
		// fields but we want to generate serialised data of a similiar form to that which would be generated if the property DID have an auto-property backing field
		/// <summary>
		/// Auto-properties have backing fields generated behind the scenes by the compiler that follow a particular convention - this will return the name of what the backing field would be for a
		/// given auto-property name. This uses the convention of the C# compiler which could technically change but it seems unlikely to since there would be a good chance of lots of small things
		/// breaking, having relied upon the undocumented format (like I am here!).
		/// </summary>
		public static string GetBackingFieldName(string propertyName)
		{
			if (string.IsNullOrWhiteSpace(propertyName))
				throw new ArgumentException($"Null/blank {nameof(propertyName)} specified");

			return PREFIX + propertyName + SUFFIX;
		}

		/// <summary>
		/// If the field is a backing field for an auto-property then return the PropertyInfo for that property, otherwise return null. This also (as GetBackingFieldName does) relies upon C#
		/// compiler conventions which hopefuly won't change
		/// </summary>
		public static PropertyInfo TryToGetPropertyRelatingToBackingField(FieldInfo field)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));

			if (!field.Name.StartsWith(PREFIX) || !field.Name.EndsWith(SUFFIX))
				return null;

			var propertyName = field.Name.Substring(PREFIX.Length, field.Name.Length - (PREFIX.Length + SUFFIX.Length));
			return field.DeclaringType.GetProperty(propertyName, BinaryReaderWriterShared.MemberRetrievalBindingFlags, null, field.FieldType, Type.EmptyTypes, null);
		}
	}
}