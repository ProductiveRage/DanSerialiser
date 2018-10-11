using System;

namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public class Stub
	{
		public int Key { get; set; }
		public TranslatedString TranslatedName { get; set; }
		public Coordinate Coordinate { get; set; }
		public int ProductType { get; set; }
		public string ProductTypeId { get; set; }
		public DateTime AmendedBySystemAt { get; set; }
		public DateTime AmendedByUserAt { get; set; }
	}
}