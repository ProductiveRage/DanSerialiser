using System;
using System.Collections.Generic;

namespace DanSerialiser
{
	internal static class TypeExtensions
	{
		/// <summary>
		/// This will return null if the Type does not implement the generic IEnumerable interface
		/// </summary>
		public static Type TryToGetIEnumerableElementType(this Type source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var enumerablesImplemented = source.FindInterfaces((t, criteria) => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IEnumerable<>)), null);
			if (enumerablesImplemented.Length == 0)
				return null;

			return enumerablesImplemented[0].GetGenericArguments()[0];
		}
	}
}