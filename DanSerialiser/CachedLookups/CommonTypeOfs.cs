using System;

namespace DanSerialiser.CachedLookups
{
	/// <summary>
	/// Caching these typeof(..) calls may help performance in some cases, as suggested here:
	///   https://rogerjohansson.blog/2016/08/16/wire-writing-one-of-the-fastest-net-serializers/
	/// I saw negligible difference but makes intuitive sense, so I'll leave it in (if only to avoid thinking about it in the future)
	/// </summary>
	internal static class CommonTypeOfs
	{
		public static readonly Type Boolean = typeof(Boolean);
		public static readonly Type Byte = typeof(Byte);
		public static readonly Type SByte = typeof(SByte);
		public static readonly Type Int16 = typeof(Int16);
		public static readonly Type Int32 = typeof(Int32);
		public static readonly Type Int64 = typeof(Int64);
		public static readonly Type UInt16 = typeof(UInt16);
		public static readonly Type UInt32 = typeof(UInt32);
		public static readonly Type UInt64 = typeof(UInt64);
		public static readonly Type Single = typeof(Single);
		public static readonly Type Double = typeof(Double);
		public static readonly Type Decimal = typeof(Decimal);
		public static readonly Type Char = typeof(Char);
		public static readonly Type String = typeof(String);
		public static readonly Type DateTime = typeof(DateTime);
		public static readonly Type TimeSpan = typeof(TimeSpan);
		public static readonly Type Guid = typeof(Guid);
		public static readonly Type Object = typeof(Object);
	}
}