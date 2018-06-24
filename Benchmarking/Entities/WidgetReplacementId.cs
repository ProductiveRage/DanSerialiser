using System;

namespace Benchmarking.Entities
{
	[Serializable]
	public class WidgetReplacementId
	{
		public string DmsExternalSystem { get; set; }
		public string Id { get; set; }
		public string IdType { get; set; }
	}
}