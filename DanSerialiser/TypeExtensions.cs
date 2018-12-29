using System;

namespace DanSerialiser
{
	internal static class TypeExtensions
	{
		public static bool IsPointer(this Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			return type.IsPointer || (type == typeof(IntPtr)) || (type == typeof(UIntPtr));
		}
	}
}