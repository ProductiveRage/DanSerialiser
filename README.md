# DanSerialiser

I want to have a way to serialise to and from a binary format in an efficient manner and for there to be some acceptable flexibility around versioning.

I want it to be possible to have a server send binary data to a client where the client has older or new versions of an object model and for it to work according to some prescribed rules.

I want it to be possible to have cached some data to disk and to reload this data after updating the binaries and the object model having changed somewhat.

## For example?

If I have a server and a client with different versions of an assembly such that one has this version of a class:

    public sealed class PersonDetails
	{
	    public PersonDetails(int id, string name)
		{
		    Id = id;
			Name = name;
		}
		
		public int Id { get; }
		
		public string Name { get; }
	}
	
.. and the other has this version:

    public sealed class PersonDetails
	{
	    public PersonDetails(int id, TranslatedString translatedName)
		{
		    Id = id;
			TranslatedName = translatedName;
		}
		
		public int Id { get; }
		
		public TranslatedString TranslatedName { get; private set; }
		
		[Deprecated(replacedByProperty: nameof(TranslatedName))]
		public string Name
		{
		    get { return TranslatedName.DefaultValue; }
			private set { TranslatedName = new TranslatedString(value, new Dictionary<int, string>()); }
		}
	}

To ensure that everything is clear, the **TranslatedName** class would look something like this:

	public sealed class TranslatedName
	{
	    public TranslatedName(string defaultValue, Dictionary<int, string> translations)
		{
		    DefaultValue = defaultValue;
			Translations = translations;
		}
		
		public string DefaultValue { get; }
		
		public Dictionary<int, string> Translations { get; }
    }
	
I don't want versioning flexibility such that data serialised from an old version of a type can be deserialised into a new version of that type and for there to be undefined behaviour about new fields or properties. It must always be well understood how everything will be populated and if there are properties on the type being deserialised to that can not be specified based upon the serialised data then it should be a hard error\*.

\* *(I don't want to be able to define an immutable type that is initialised by a constructor that ensures that every property is set to a non-null value and for the deserialiser to be able to side step that and create an instance that has properties with null values because that will lead to confusion at some point down the road)*

For the simplest cases, I don't want to have to add any special attributes or interfaces or to have to compromise on how types are structured. If I want to serialise / deserialise a POCO using this, then I should be able to. If I want to serialise / deserialise an immutable type using this, then I should be able to. If I want to support backwards / forwards compatibility across versions of types when serialising / deserialising then some additional annotations will be acceptable (such as the "Deprecated" attribute in the example above) but they shouldn't be necessary otherwise.

## Bonus points

This should result in a NuGet package that works with .NET Framework and with .NET Standard / Core.

Its performance should at least be compatible with obvious alternatives such as the BinaryFormatter. While performance is not the number one goal, it should not be forgotten. Maybe it won't be as fast as [protobuf](https://github.com/google/protobuf/tree/master/csharp) / [protobuf-net](https://github.com/mgravell/protobuf-net) or [ZeroFormatter](https://github.com/neuecc/ZeroFormatter) but it shouldn't be much *much* slower. The priority is on making it fast-*ish* and on making it easy to use and on supporting versioning in a way that I am happy with. But performance is always something fun to play with after the core functionality is ready!