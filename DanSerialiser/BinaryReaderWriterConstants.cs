using System.Reflection;

namespace DanSerialiser
{
	internal static class BinaryReaderWriterConstants
	{
		public const string FieldTypeNamePrefix = "#type#";
		public static readonly BindingFlags FieldRetrievalBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
	}
}