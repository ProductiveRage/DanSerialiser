using System.Collections.Generic;
using DanSerialiser;

namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public sealed class TranslatedString
	{
		public string Default { get; set; }

		// There is a note in ReadMe.txt about the use of this attribute and more notes in the summary comment for the attribute
		[SpecialisationsMayBeIgnoredWhenSerialising]
		public Dictionary<string, string> Translations { get; set; }
	}
}