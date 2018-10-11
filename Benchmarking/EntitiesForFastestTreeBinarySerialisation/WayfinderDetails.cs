using System;

namespace Benchmarking.EntitiesForFastestTreeBinarySerialisation
{
	public sealed class WayfinderDetails
	{
		public int Key { get; set; }
		public TimeSpan? MinimumDuration { get; set; }
		public TimeSpan? MaximumDuration { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public int? LengthInMetres { get; set; }
		public int? GradeKey { get; set; }
		public string GradeId { get; set; }
		public string GradeName { get; set; }
		public int? TypeKey { get; set; }
		public string TypeId { get; set; }
		public string TypeName { get; set; }
		public int DetailLevelKey { get; set; }
		public string DetailLevelId { get; set; }
		public string DetailLevelName { get; set; }
		public WayFinderPoint[] Points { get; set; }
		public WayFinderRelatedProductDetails[] RelatedProducts { get; set; }

		public sealed class WayFinderPoint
		{
			public int Key { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
			public string Name { get; set; }
			public string Image { get; set; }
		}

		public sealed class WayFinderRelatedProductDetails
		{
			public DateTime Amended { get; set; }
			public AssociationTypeOptions AssociationType { get; set; }
			public int? CategoryKeyIfAny { get; set; }
			public string CategoryIdIfAny { get; set; }
			public Coordinate CoordinatesIfAny { get; set; }
			public int Key { get; set; }
			public int ProdTypeKey { get; set; }
			public string ProdTypeId { get; set; }
			public int Sequence { get; set; }
			public TranslatedString TranslatedName { get; set; }

			public enum AssociationTypeOptions { EndPoint, MidPoint, StartPoint }
		}
	}
}