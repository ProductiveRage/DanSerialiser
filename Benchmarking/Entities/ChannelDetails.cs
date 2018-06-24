using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class ChannelDetails
	{
		public int Key { get; set; }
		public string ChannelCode { get; set; }
		public int ListingLevel { get; set; }
		public bool KeyProduct { get; set; }
		public bool DoNotIndex { get; set; }
	}
}