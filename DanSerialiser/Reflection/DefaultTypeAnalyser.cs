namespace DanSerialiser.Reflection
{
	internal static class DefaultTypeAnalyser
	{
		public static IAnalyseTypesForSerialisation Instance { get; } = new CachingTypeAnalyser(ReflectionTypeAnalyser.Instance);
	}
}