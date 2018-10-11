namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public sealed class AddressDetails
	{
		public TranslatedString TranslatedAddress1 { get; set; }
		public TranslatedString TranslatedAddress2 { get; set; }
		public TranslatedString TranslatedAddress3 { get; set; }
		public TranslatedString TranslatedAddress4 { get; set; }
		public TranslatedString TranslatedAddress5 { get; set; }
		public TranslatedString TranslatedPostCode { get; set; }
		public string Country { get; set; }
		public string CountryCode { get; set; }
		public int? CountryKey { get; set; }
	}
}