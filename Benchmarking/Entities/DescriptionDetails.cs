using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class DescriptionDetails
	{
		public int Language { get; set; }
		public bool Default { get; set; }
		public string[] Channels { get; set; }
		public string Short { get; set; }
		public string Long { get; set; }
		public string Rich { get; set; }
	}
}