using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class UnitDetails
	{
		public int Key { get; set; }
		public string AvailabilityStatement { get; set; }
		public DayOptions AvailabilityStartDay { get; set; }
		public int? AvailabilityMaxDuration { get; set; }
		public bool IsBookableUnit { get; set; }
		public bool IsConferenceRoom { get; set; }
		public bool ConsiderUnitWhenCalculatingGuidePrices { get; set; }
		public int MinOccupancy { get; set; }
		public int Capacity { get; set; }
		public int Quantity { get; set; }
		public int UnitType { get; set; }
		public string UnitTypeId { get; set; }
		public string UnitTypeName { get; set; }
		public PriceDetailsWithBasis GuidePrice { get; set; }
		public UnitDetails[] LinkedUnits { get; set; }
		public OpeningDetails[] Openings { get; set; }
		public TranslatedString TranslatedName { get; set; }
		public TranslatedString TranslatedNotes { get; set; }

		[Serializable]
		public class OpeningDetails
		{
			public DateTime? Start { get; set; }
			public DateTime? End { get; set; }
			public PriceDetailsWithBasis MinPrice { get; set; }
			public PriceDetailsWithBasis MaxPrice { get; set; }
		}

		[Serializable]
		public class PriceDetailsWithBasis
		{
			public decimal Value { get; set; }
			public string Currency { get; set; }
			public string PriceBasisId { get; set; }
			public string PriceBasis { get; set; }
			public int? PriceBasisKey { get; set; }
		}

		[Serializable]
		public abstract class PriceDetailsBase
		{
			public decimal Value { get; set; }
			public string Currency { get; set; }
		}

		public enum DayOptions : byte { Unknown, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday, BankHoliday }
	}
}