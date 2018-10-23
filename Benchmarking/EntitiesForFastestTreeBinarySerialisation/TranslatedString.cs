namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public sealed class TranslatedString
	{
		public string Default { get; set; }

		public SealedDictionary<string, string> Translations { get; set; }
	}
}