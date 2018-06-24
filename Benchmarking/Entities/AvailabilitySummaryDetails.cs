using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class AvailabilitySummaryDetails
	{
		public DateTime MaxAvailDate { get; set; }
		public DateTime? MaxIndicativeAvailDate { get; set; }
		public bool SelfCatExt { get; set; }
		public bool HasChildPricing { get; set; }
	}
}