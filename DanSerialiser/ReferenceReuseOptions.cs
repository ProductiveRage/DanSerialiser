namespace DanSerialiser
{
	public enum ReferenceReuseOptions
	{
		/// <summary>
		/// This would seem to be the best fit for a classic tree structure where no circular references are possible.. however, it seems like the SupportReferenceReUseInMostlyTreeLikeStructure
		/// is able to give better performance currently (which is why this is not required in any public constructor at this time - because it's not an obvious choice for a consumer of the library
		/// what option would be best for them to select)
		/// </summary>
		NoReferenceReuse,

		/// <summary>
		/// This is the best option for object models that are essentially tree-like in nature but there is some chance that some branches will have references further back up the tree (circular /
		/// reused reference). This currently gives the best performance for tree structures.
		/// </summary>
		SupportReferenceReUseInMostlyTreeLikeStructure,

		/// <summary>
		/// If the object model has large arrays where each element is the head of a circular reference then using SupportReferenceReUseInMostlyTreeLikeStructure may result in a stack overflow
		/// exception due to the way in which the structure is investigated. This configuration will take a breadth-wise approach to identifying object references in arrays and so may avoid the
		/// stack overflows but there are optimisations which may not be applied in this case and so this should probably not be the default approach.
		/// </summary>
		OptimiseForWideCircularReferences,

		/// <summary>
		/// This option may be used where raw performance is more important than other factors, such as detecting circular references (meaning circular references in an object graph to be serialised
		/// will result in a stack overflow exception) and supporting custom type converters. The backwards and forwards compatibility support for different versions of serialisation entities will
		/// still be fully supported since that will always be a primary goal of this library, more important than performance.
		/// </summary>
		SpeedyButLimited
	}
}