using System;
using System.Collections.Generic;

namespace Benchmarking.Entities
{
	[Serializable]
	public class TranslatedString
	{
		public string Default { get; set; }
		public Dictionary<string, string> Translations { get; set; }
	}
}