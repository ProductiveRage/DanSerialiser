These serialisation entities are a slight variation on those in the "Entities" folder, altered such that the FastestTreeBinarySerialisation will be able to apply more optimisations (so that we can compare its performance
against the regular serialisation entities and these ones).

The only changes are that now:

  1. The classes are sealed
  2. The UnitDetails-property-within-UnitDetails-class arrangement has been removed
     - In the data model that I based the sample data from, "units" can have "linked units" but those child units could not have any linked units themselves. I changed UnitDetails into an abstract class and removed
       the "LinkedUnits" property from it, adding it to a sealed "PhysicalUnitDetails" that derived from the "UnitDetails" base class. This new property is of type "AliasUnitDetails[]" and AliasUnitDetails is a new
       sealed class that is derived from "UnitDetails" but that doesn't add any new properties. 
  3. The Dictionary<string, string> property in the "TranslatedString" class has had the [SpecialisationsMayBeIgnoredWhenSerialising] applied to indicate that the serialiser may act as if that type is sealed because
     the object model does not require any specialisations of the dictionary for that property, nor will it ever require an equality comparer other than the default.
  
The changes for points 1 and 2 allow more optimisations to be made during serialisation but they also, arguably, improve the data model.

Point 3 is much more specific to the serialisation process. With these classes, it would be possible to set a dictionary reference that did specify a non-default equality comparer or to set the "Translations" property
to be an instance of MyStringDictionary (a class that is derived from Dictionary<string, string>) and this information would be lost by the serialisation process due to the [SpecialisationsMayBeIgnoredWhenSerialising]
attribute. If this "discount the possibility of specialisations" option would not be applicable to your data then do not use the [SpecialisationsMayBeIgnoredWhenSerialising] attribute - serialisation will still be
possible but there are some optimisations that won't be applied and so it will be a little slower.