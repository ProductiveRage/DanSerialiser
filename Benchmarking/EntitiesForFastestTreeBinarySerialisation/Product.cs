using System;

namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public sealed class Product : Stub
	{
		public AddressDetails Address { get; set; }
		public AvailabilitySummaryDetails AvailabilitySummary { get; set; }
		public AwardDetails[] Awards { get; set; }
		public CategoryDetails[] Categories { get; set; }
		public ChannelDetails[] Channels { get; set; }
		public ContactDetails Contact { get; set; }
		public DateTime Created { get; set; }
		public DateTime DataLoadedAt { get; set; }
		public DescriptionDetails[] Descriptions { get; set; }
		public TranslatedString TranslatedKeywords { get; set; }
		public int[] Polygons { get; set; }
		public int PlaceKey { get; set; }
		public string PlaceName { get; set; }
		public TranslatedString TranslatedPricingText { get; set; }
		public DescriptionDetails[] PublicDirections { get; set; }
		public Guid RandomOrder24Hr { get; set; }
		public string RemoteId { get; set; }
		public DescriptionDetails[] RoadDirections { get; set; }
		public string Source { get; set; }
		public PhysicalUnitDetails[] Units { get; set; }
		public WayfinderDetails[] Wayfinders { get; set; }
		public WidgetReplacementId[] WidgetReplacementIds { get; set; }
	}
}