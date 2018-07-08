# DanSerialiser

I want to have a way to serialise to and from a binary format in an efficient manner and for there to be some acceptable flexibility around versioning.

I want it to be possible to have a server send binary data to a client where the client has older or new versions of an object model and for it to work according to some prescribed rules.

I want it to be possible to have cached some data to disk and to reload this data after updating the binaries and the object model having changed somewhat.

For the simplest cases, I don't want to have to add any special attributes or interfaces or to have to compromise on how types are structured. If I want to serialise / deserialise a POCO using this, then I should be able to. If I want to serialise / deserialise an immutable type using this, then I should be able to. If I want to support backwards / forwards compatibility across versions of types when serialising / deserialising then some additional annotations will be acceptable but they shouldn't be necessary otherwise.

Well, this library meets those requirements! It's written using .NET Standard 2.0, which means that it can be used by both .NET Framework 4.6.1+ *and* .NET Standard 2.0+.

Using it is as simple as this:

    // Get a byte array
    var serialisedData = DanSerialiser.BinarySerialisation.Serialise(value);
    
    // Deserialise the byte array back into an object
    var result = DanSerialiser.BinarySerialisation.Deserialise<PersonDetails>(serialisedData);
    
The above are static convenience methods that serialise to and from a byte array via a MemoryStream using code like the following:

    // Serialise to a strem
    var writer = new BinarySerialisationWriter(stream);
    Serialiser.Instance.Serialise(value, writer);

    // Deserialise data from the stream back into an object
    var result = (new BinarySerialisationReader(stream)).Read<PersonDetails>();
    
.. and so, if you want to use a different stream (to read/write straight from/to disk, for example) then you can do so easily.
    
## "backwards / forwards compatibility" examples?

Say I have an internal API that returns the following class:

    public sealed class PersonDetails
    {
        public PersonDetails(int id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        public int Id { get; }
        
        public string Name { get; }
    }

Then, one day, I need to change the "Name" field such that it can have different string values for different languages - like this:

    public sealed class PersonDetails
    {
        public PersonDetails(int id, TranslatedString translatedName)
        {
            Id = id;
            TranslatedName = translatedName ?? throw new ArgumentNullException(nameof(translatedName));
        }
        
        public int Id { get; }
        
        public TranslatedString TranslatedName { get; private set; }
    }

    public sealed class TranslatedString
    {
        public TranslatedString(string defaultValue, Dictionary<int, string> translations)
        {
            DefaultValue = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));
            Translations = translations ?? throw new ArgumentNullException(nameof(translations));
        }
        
        public string DefaultValue { get; }
        
        public Dictionary<int, string> Translations { get; }
    }
    
I update the server code so that it's using this new version of the entity but now I have to update all of the clients so that they know about the new **PersonDetails** format - until I do that, the clients are broken. This is annoying because sometimes I'd like to roll out changes to services one-by-one instead of requiring a "big bang" update involving *all* related services.

What this library allows me to do is add some properties that can make a type "backwards compatible" in terms of serialisation - eg.
    
    public sealed class PersonDetails
    {
        public PersonDetails(int id, TranslatedString translatedName)
        {
            Id = id;
            TranslatedName = translatedName ?? throw new ArgumentNullException(nameof(translatedName));
        }
        
        public int Id { get; }
        
        public TranslatedString TranslatedName { get; }
        
        [Deprecated]
        public string Name
        {
            get { return TranslatedName.DefaultValue; }
        }
    }
    
The "Name" property on the updated version of this class has two purposes. The obvious one is that when I rebuild my client project against this updated entity class, the code will still compile - though none of the translations for the name values will be used, everywhere will show the DefaultValue (this may or may not be desirable, I'll touch on this again in a moment). The second benefit is that this serialiser will use that [Deprecated] property annotation to generate binary data that may be deserialised into the new version of the entity *or* the old version. This means that clients that reference the old version of the entity class can deserialise data from the server when the server is referencing the *new* version of the entity. This allows me to update my API service first, without worrying about the clients breaking.. and then I can update the clients at a later date.

That's what I'm referring to as "backwards compatible" serialisation support - serialised data from a new version of an entity can be deserialised as an older version of the entity.

However, a similar problem can occur the other way around. What if the client needs to be able to send data to the server, when the server is referencing the newer version of **PersonDetails** and the client is referencing the older one? That can also be handled, in a similar manner:

    public sealed class PersonDetails
    {
        public PersonDetails(int id, TranslatedString translatedName)
        {
            Id = id;
            TranslatedName = translatedName ?? throw new ArgumentNullException(nameof(translatedName));
        }
        
        public int Id { get; }
        
        public TranslatedString TranslatedName { get; private set; }
        
        [Deprecated(replacedBy: nameof(TranslatedName))]
        public string Name
        {
            get { return TranslatedName.DefaultValue; }
            private set { TranslatedName = new TranslatedString(value, new Dictionary<int, string>()); }
        }
    }

The [Deprecated] property now has a private setter. This can never be called by regular code (because it's private) but the serialiser can use it when the [Deprecated] attribute has a "replacedBy" value. If an old version of **PersonDetails** is serialised and then deserialised into a *new* version of **PersonDetails** then the "Name" value from the old-format data will be used to populate the "TranslatedName" property.

This is what I'm referring to as "forwards compatible" serialisation support.

As well as for communications between services, this feature could be useful for when serialised data is cached externally - such as on disk or in Redis. A service could flush expensive-to-generate data to an external cache before the service is updated and then - after the update - still read back that serialised data despite the fact that the data was generated from older versions of some of the entities. This cached data may then need to be rebuilt so that the new information can be pulled through (in the example above, **PersonDetails** instances that were cached would want to be rebuilt so that the translated name data would start coming in) - however, this could be done at a gentle pace if stale data is acceptable for a short period of time. The alternative would be that the service would come back up, be unable to read the data serialised by its older self and then have to get all of the data fresh immediately, which could put significant load on the server. Some services are designed such that they should *never* serve stale data and so this might not be useful and many services are designed such that they can operate without a well-populated cache (they will work much more quickly with a warm cache but it's not essential) but *some* services are unable to run acceptably under load with an empty cache and this could come in very handy in those cases.

I said above that the [Deprecated] property being public might be seen as a benefit - because upgrading an assembly such that the new version of **PersonDetails** is referenced won't break your existing code that reads the "Name" property. You may disagree and say "no, when I upgrade and get the new version of **PersonDetails**, I *want* to have to change all of the references to the 'Name' property because everywhere *should* use 'TranslatedName' now". Well that is fine too, you can make the property private and then the serialiser can still use it but regular code won't:

    public sealed class PersonDetails
    {
        public PersonDetails(int id, TranslatedString translatedName)
        {
            Id = id;
            TranslatedName = translatedName ?? throw new ArgumentNullException(nameof(translatedName));
        }
        
        public int Id { get; }
        
        public TranslatedString TranslatedName { get; private set; }
        
        // Private property - only for serialisation backwards/forward version compatibility
        [Deprecated(replacedBy: nameof(TranslatedName))]
        private string Name
        {
            get { return TranslatedName.DefaultValue; }
            set { TranslatedName = new TranslatedString(value, new Dictionary<int, string>()); }
        }
    }

## Reliability
    
It's worth noting that I don't want versioning "flexibility" such that data may be deserialised into a type and for there to be some kind of undefined behaviour. It must always be well understood how everything will be populated and if there are properties on the type being deserialised to that can not be populated from the serialised data then it should be a hard error\*.

\* *(For example, I don't want to be able to define an immutable type that is initialised by a constructor that ensures that every property is set to a non-null value and for the deserialiser to be able to side step that and create an instance that has properties with null values because that is very likely to confusion at some point down the road)*

For example, if I tried to deserialise from the old version of **PersonDetails** -

    public sealed class PersonDetails
    {
        public PersonDetails(int id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        public int Id { get; }
        
        public string Name { get; }
    }

.. into a new version that *doesn't* have the [Deprecated] attribute on its "TranslatedName" property -

    public sealed class PersonDetails
    {
        public PersonDetails(int id, TranslatedString translatedName)
        {
            Id = id;
            TranslatedName = translatedName ?? throw new ArgumentNullException(nameof(translatedName));
        }
        
        public int Id { get; }
        
        public TranslatedString TranslatedName { get; }
    }

.. then there will be a runtime error. There is no way for the serialiser to set the "TranslatedName" property. I would never expect to see an instance of **PersonDetails** with a null "TranslatedName" value because the constructor won't allow it - if the serialiser allowed us to create an instance of **PersonDetails** with a null "TranslatedName" value then I could get a very nasty surprise elsewhere in my system!

There is one more attribute to cover. Sometimes, in a "forward compatible" deserialisation scenario, you *want* it be acceptable for some properties not to be set. For example, if the disk cached data has an instance of this class:

    public sealed class PersonDetails
    {
        public PersonDetails(int id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        public int Id { get; }
        
        public string Name { get; }
    }
    
.. and, since that data was written, a new property has been added to **PersonDetails** then you might want to tell the serialiser "don't worry if there is no data for this". That may be done using the [OptionalWhenDeserialisingAttribute] attribute -

    public sealed class PersonDetails
    {
        public PersonDetails(int id, string name, DateTime? lastKnownContact)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LastKnownContact = lastKnownContact;
        }
        
        public int Id { get; }
        
        public string Name { get; }
        
        [OptionalWhenDeserialisingAttribute]
        public DateTime? LastKnownContact { get; }
    }

Without this attribute, the deserialisation would fail - it is not acceptable for nullable or reference-type values to be left as null if there is no content for them in serialised data *unless* they are marked as [OptionalWhenDeserialisingAttribute] (this attribute may also be used with value types, in which case the property or field will be left as the default(T) for that type).

## Circular references

I like my data to be represented by immutable structures unless there's a really compelling reason not to. These generally (though not *always*) avoid circular references by being tree-like in nature. However, should you want to serialise data that contains circular references with this library then you can. If we take this class, for example:

    public class PersonDetails
    {
        public int Id { get; set; }
        
        public string Name { get; set; }
        
        public PersonDetail BestFriend { get; set; }
    }
    
.. and whip up a couple of inter-connected instances:

    var tim = new PersonDetails { Id = 1, Name = "Tim" };
    var bob = new PersonDetails { Id = 2, Name = "Bob", BestFriend = tim };
    tim.BestFriend = bob;
    
.. then these may be serialised / deserialised without issue.

This works but I have seen it fail (with a stack overflow exception) if the object model has array properties that are very wide and where each element is the start of a chain that ends with a circular reference. The serialiser approaches data as if it is a tree and so this arrangement will effectively add layers to the stack trace for every element in the array. If these array elements are object references that have properties that are *also* arrays then the problem gets even worse, quickly! I've had some thoughts about ways to try to tackle structures such as this but I don't have anything that I'm happy with yet.

## Performance

My primary goal with this library was to see if I could create something that fit the versioning plan that I had in mind. Performance is not the number one goal - but it also shouldn't be forgotten. It won't be as fast as [protobuf](https://github.com/google/protobuf/tree/master/csharp) / [protobuf-net](https://github.com/mgravell/protobuf-net) but it also shouldn't require their compromises\*. It should be at least comparable to obvious alternatives, such as the BinaryFormatter.

\* *(protobuf-net would ideally have attributes on every type and every field/property that should be serialisable - this can be done programatically to some extent but it doesn't seem like it's possible to support circular references without using attributes; it also doesn't seem to differentiate between empty lists and null list references and that's not something that I'm particularly happy about)*

There is a project in the repository that uses [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) and sample data that matches the primary use case that I had in mind when I started this project. On my computer, the results are currently as follows:

|                     Method |  Job | Runtime |      Mean |     Error |    StdDev | Compared to DanSerialiser
|--------------------------- |----- |-------- |----------:|----------:|----------:|
|           JsonNetSerialise |  Clr |     Clr | 152.39 ms | 0.8945 ms | 0.7929 ms | 2.7x slower
|         JsonNetDeserialise |  Clr |     Clr | 128.61 ms | 0.7112 ms | 0.6652 ms | 3.5x slower
|   BinaryFormatterSerialise |  Clr |     Clr |  89.68 ms | 0.7623 ms | 0.6757 ms | 1.6x slower
| BinaryFormatterDeserialise |  Clr |     Clr | 128.53 ms | 1.2025 ms | 1.1248 ms | 3.5x slower
|          ProtoBufSerialise |  Clr |     Clr |  11.52 ms | 0.1714 ms | 0.1338 ms | 4.9x faster
|        ProtoBufDeserialise |  Clr |     Clr |  21.70 ms | 0.1375 ms | 0.1286 ms | 1.7x faster
|     DanSerialiserSerialise |  Clr |     Clr |  56.29 ms | 0.1879 ms | 0.1758 ms | -
|   DanSerialiserDeserialise |  Clr |     Clr |  37.09 ms | 0.7141 ms | 0.7333 ms | -
|           JsonNetSerialise | Core |    Core |  98.05 ms | 0.8354 ms | 0.7814 ms | 2.1x slower
|         JsonNetDeserialise | Core |    Core | 129.66 ms | 0.3665 ms | 0.3249 ms | 3.7x slower
|   BinaryFormatterSerialise | Core |    Core | 105.34 ms | 0.7633 ms | 0.7140 ms | 2.2x slower
| BinaryFormatterDeserialise | Core |    Core | 135.73 ms | 0.8164 ms | 0.6818 ms | 3.8x slower
|          ProtoBufSerialise | Core |    Core |  10.26 ms | 0.1192 ms | 0.0996 ms | 4.6x faster
|        ProtoBufDeserialise | Core |    Core |  21.35 ms | 0.1531 ms | 0.1358 ms | 1.7x faster
|     DanSerialiserSerialise | Core |    Core |  47.44 ms | 0.4564 ms | 0.4269 ms | -
|   DanSerialiserDeserialise | Core |    Core |  35.46 ms | 0.3907 ms | 0.3463 ms | -

Initially, I imagined that getting with one order of magnitude of protobuf would be acceptable but I hadn't realised how close Json.NET would be in performance - approx. 13.2x / 9.6x times slower than protobuf to serialise the data on .NET 4.6.1 / .NET Core 2.1 and only 5.9x / 6.0x slower to deserialise it. Considering that Json.NET is so general purpose, I thought that that was impressive!

I'm happy that *this* library is less than 5x as slow as protobuf in serialising the sample data and less than 2x as slow at deserialising.