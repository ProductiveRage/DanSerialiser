These serialisation entities are a slight variation on those in the "Entities" folder, altered such that the FastestTreeBinarySerialisation will be able to apply more optimisations (so that we can compare its performance
against the regular serialisation entities and these ones).

The only changes are that now:

  1. The classes are sealed
  2. The UnitDetails-property-within-UnitDetails-class arrangement has been removed
     - In the data model that I based the sample data from, "units" can have "linked units" but those child units could not have any linked units themselves. I changed UnitDetails into an abstract class and removed
       the "LinkedUnits" property from it, adding it to a sealed "PhysicalUnitDetails" that derived from the "UnitDetails" base class. This new property is of type "AliasUnitDetails[]" and AliasUnitDetails is a new
       sealed class that is derived from "UnitDetails" but that doesn't add any new properties. 
  3. The Dictionary<string, string> property in the "TranslatedString" class has been changed to be a SealedDictionary<string, string> which is a sealed class that is derived from Dictionary<string, string> that can
     not be given a custom equality comparer, which allows the DefaultEqualityComparerFastSerialisationTypeConverter to be used so that there are no 'unknown' types for the pre-serialisation optimisation pass to get
	 confused by.
  
The changes for points 1 and 2 allow more optimisations to be made during serialisation but they also, arguably, improve the data model.

Point 3 is more specific to THIS serialisation process, it wouldn't work if there was data elsewhere in the object graph being serialised that required support for other IEqualityComparer<T> implementations. If this
optimisation is not possible then the serialisation will still complete successfully but some of the optimisations won't be available (with this model, each Product instance will be serialised with a single call to a
compiled LINQ expression - which is the best case for performance - but if some fields or properties have types that can't be known ahead of the actual serialisation then some types will be written using compiled
expressions and some will be processed by logic in the Serialiser class; with thie hybrid approach, the more types that can be written by compiled expressions, the quicker it should be).