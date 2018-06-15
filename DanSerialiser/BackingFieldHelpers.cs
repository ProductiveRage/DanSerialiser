using System;

namespace DanSerialiser
{
	internal sealed class BackingFieldHelpers
	{
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

			return "<" + propertyName + ">k__BackingField";
		}
	}
}