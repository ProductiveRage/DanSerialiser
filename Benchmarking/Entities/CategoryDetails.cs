using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class CategoryDetails
	{
		public int Key { get; set; }
		public bool IsPrimary { get; set; }
		public bool IsSystem { get; set; }
		public string Id { get; set; }
		public string Description { get; set; }
	}
}