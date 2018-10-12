# DanSerialiser

I want to have a way to serialise to and from a binary format in an efficient manner and for there to be some acceptable flexibility around versioning.

I want it to be possible to have a server send binary data to a client where the client has older or new versions of an object model and for it to work according to some prescribed rules.

I want it to be possible to have cached some data to disk and to reload this data after updating the binaries and the object model having changed somewhat.

For the simplest cases, I don't want to have to add any special attributes or interfaces or to have to compromise on how types are structured. If I want to serialise / deserialise a POCO using this, then I should be able to. If I want to serialise / deserialise an immutable type using this, then I should be able to. If I want to support backwards / forwards compatibility across versions of types when serialising / deserialising then some additional annotations will be acceptable but they shouldn't be necessary otherwise.

Well, this library meets those requirements! It's written using .NET Standard 2.0, which means that it can be used by both .NET Framework 4.6.1+ *and* .NET Standard 2.0+. (Get it from NuGet! [www.nuget.org/packages/DanSerialiser](https://www.nuget.org/packages/DanSerialiser/))

Using it is as simple as this:

    // Get a byte array
    var serialisedData = DanSerialiser.BinarySerialisation.Serialise(value);
    
    // Deserialise the byte array back into an object
    var result = DanSerialiser.BinarySerialisation.Deserialise<PersonDetails>(serialisedData);
    
The above are static convenience methods that serialise to and from a byte array via a MemoryStream using code like the following:

    // Serialise to a strem
    var writer = new DanSerialiser.BinarySerialisationWriter(stream);
    Serialiser.Instance.Serialise(value, writer);

    // Deserialise data from the stream back into an object
    var result = (new DanSerialiser.BinarySerialisationReader(stream)).Read<PersonDetails>();
    
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
        
        public TranslatedString TranslatedName { get; }
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

For many cases, this will work without issue. However, there are some shapes of object model that don't fit nicely, such as when a large array exists where the elements are all the start of circular reference chains. The serialiser approaches data as if it is a tree and so this arrangement will effectively add layers to the stack trace for every element in the array. If these array elements are object references that have properties that are *also* arrays (whose elements are part of circular reference chains) then the problem gets even worse, quickly! You'll know that you have a problem because attempting to serialise the data will result in a stack overflow exception.

There is a way to tackle this kind of data; the "optimiseForWideCircularReference" argument that exists on the static **BinarySerialisation**.Serialise method and as a constructor argument on the **BinarySerialisationWriter** - it defaults to false but, if set to true, then it will serialise array data in a "breadth first" manner such that the changes of a stack overflow are greatly reduced. Unfortunately, there is some additional analysis that the serialisation process must do and there are some optimisations that the deserialisation process can not apply and so using this option comes with a cost. The "Performance" section below has figures to compare the execution times when this option ("optimised for wide circular references") is enabled vs when it's not (when it is enabled, the serialisation and deserialisation processes are still faster than Json.NET and the BinaryFormatter but not by as much).

*(Note: There is no flag to tell the deserialisation process - whether you call the static **BinarySerialisation.Deserialise** method or if you instantiate a **BinarySerialisationReader** for a stream - because the serialised data contains information about how it is was written and whether the option was enabled)*

I would recommend *not* enabling this option unless you come to find that you have to.

## Performance

My primary goal with this library was to see if I could create something that fit the versioning plan that I had in mind. Performance is not the number one goal - but it also shouldn't be forgotten. It won't be as fast as [protobuf](https://github.com/google/protobuf/tree/master/csharp) / [protobuf-net](https://github.com/mgravell/protobuf-net) but it also shouldn't require their compromises\*. It should be at least comparable to obvious alternatives, such as the BinaryFormatter.

\* *(protobuf-net would ideally have attributes on every type and every field/property that should be serialisable - this can be done programatically to some extent but it doesn't seem like it's possible to support circular references without using attributes; it also doesn't seem to differentiate between empty lists and null list references and that's not something that I'm particularly happy about)*

There is a project in the repository that uses [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) and sample data that matches the primary use case that I had in mind when I started this project. On my computer, the results are currently as follows:

|                                                            Method |  Job | Runtime |      Mean |     Error |    StdDev | Compared to DanSerialiser |
|------------------------------------------------------------------ |:----:|:-------:|----------:|----------:|----------:|---------------------------|
|                                                  JsonNetSerialise |  Clr |     Clr | 150.48 ms | 1.0164 ms | 0.9010 ms | 2.8x slower               |
|                                          BinaryFormatterSerialise |  Clr |     Clr |  86.14 ms | 1.0413 ms | 0.9740 ms | 1.6x slower               |
|   DanSerialiserSerialise (optimised for wide circular references) |  Clr |     Clr |  70.10 ms | 0.6726 ms | 0.6292 ms | 1.3x slower               |
|                                            DanSerialiserSerialise |  Clr |     Clr |  53.07 ms | 0.2024 ms | 0.1690 ms | -                         |
|                                                 ProtoBufSerialise |  Clr |     Clr |  11.08 ms | 0.2123 ms | 0.2085 ms | **4.8x faster**           |

|                                                            Method |  Job | Runtime |      Mean |     Error |    StdDev | Compared to DanSerialiser |
|------------------------------------------------------------------ |:----:|:-------:|----------:|----------:|----------:|---------------------------|
|                                                JsonNetDeserialise |  Clr |     Clr | 128.86 ms | 2.1108 ms | 1.8712 ms | 3.4x slower               |
|                                        BinaryFormatterDeserialise |  Clr |     Clr | 126.07 ms | 0.8547 ms | 0.7137 ms | 3.4x slower               |
| DanSerialiserDeserialise (optimised for wide circular references) |  Clr |     Clr | 129.83 ms | 0.7201 ms | 0.6735 ms | 2.4x slower               |
|                                          DanSerialiserDeserialise |  Clr |     Clr |  37.42 ms | 0.6876 ms | 0.6432 ms | -                         |
|                                               ProtoBufDeserialise |  Clr |     Clr |  21.43 ms | 0.0683 ms | 0.0533 ms | **1.7x faster**           |

|                                                            Method |  Job | Runtime |      Mean |     Error |    StdDev | Compared to DanSerialiser |
|------------------------------------------------------------------ |:----:|:-------:|----------:|----------:|----------:|---------------------------|
|                                          BinaryFormatterSerialise | Core |    Core | 102.03 ms | 0.9795 ms | 0.9162 ms | 2.2x slower               |
|                                                  JsonNetSerialise | Core |    Core |  94.39 ms | 0.8056 ms | 0.6727 ms | 2.1x slower               |
|   DanSerialiserSerialise (optimised for wide circular references) | Core |    Core |  61.84 ms | 0.4467 ms | 0.4178 ms | 1.4x slower               |
|                                            DanSerialiserSerialise | Core |    Core |  45.80 ms | 0.6506 ms | 0.6086 ms | -                         |
|                                                 ProtoBufSerialise | Core |    Core |  10.11 ms | 0.0346 ms | 0.0289 ms | **4.5x faster**           |

|                                                            Method |  Job | Runtime |      Mean |     Error |    StdDev | Compared to DanSerialiser |
|------------------------------------------------------------------ |:----:|:-------:|----------:|----------:|----------:|---------------------------|
|                                        BinaryFormatterDeserialise | Core |    Core | 134.02 ms | 0.8240 ms | 0.7305 ms | 3.6x slower               |
|                                                JsonNetDeserialise | Core |    Core | 128.48 ms | 0.5947 ms | 0.5272 ms | 3.5x slower               |
| DanSerialiserDeserialise (optimised for wide circular references) | Core |    Core | 127.16 ms | 1.4280 ms | 1.3358 ms | 2.8x slower               |
|                                          DanSerialiserDeserialise | Core |    Core |  37.08 ms | 0.1632 ms | 0.1274 ms | -                         |
|                                               ProtoBufDeserialise | Core |    Core |  20.19 ms | 0.1603 ms | 0.1421 ms | **1.8x faster**           |

Initially, I imagined that getting with one order of magnitude of protobuf would be acceptable but I hadn't realised how close Json.NET would be in performance - approx. 13.6x / 9.3x times slower than protobuf to serialise the data on .NET 4.6.1 / .NET Core 2.1 and only 6.0x / 6.4x slower to deserialise it. Considering that Json.NET is so general purpose, I thought that that was impressive!

I'm happy that *this* library is less than 5x as slow as protobuf in serialising the sample data and less than 2x as slow at deserialising.

## A words about enums and entity versioning

Enums in C# are a bit of a wobbly concept, they look like they might bring strong-typing to the game than they really do. For example, if we take this enum:

	// https://thedailywtf.com/articles/What_Is_Truth_0x3f_
	enum Bool { True, False, FileNotFound }

.. then we can set values like this:

	var isTrue = Bool.True;
	var isFalse = Bool.False;

.. but we can also do this:

	var problem99 = (Bool)99;

.. and so there is no guarantee that a **Bool** variable will have a value that is one of the enum's named options.

The default behaviour of this serialiser is to write the underlying numeric value for enums. This means that it is possible to get enum values in deserialised content that are not valid. For example, if the enum above was from v1 of an assembly but it was extended in v2 to this:

	enum Bool { True, False, FileNotFound, SurpriseMe }

.. and an entity from the v2 assembly was serialised that contained the value **Bool**.SurpriseMe and then *deserialised* in a process where the v1 assembly was loaded then the value would be (**Bool**)3, which would not be named.

Serialising the underlying values for enums is expected to result in the least surprising behaviour in cases where enums vary across assembly versions (particularly since enums in C# are not limited to the declared named values in normal operation).

If you would prefer to change this behaviour for your use case then you may do so using Type Converters.

## "Type Converters" for changing the shape of data while in transit

There may be occasions where you wish to change how data is represented in its serialised state - not changes to the entity types being serialised but a way to control how they are transmitted. One example would be if you wanted to change how versioning of enum values is handled. For example, if you had the following enum in v1 of an assembly:

	enum FailureReason { Unknown, FileNotFound, AccessDenied, TimeOut }
	
.. and then expanded it in v2 of that assembly to this:

	enum FailureReason { Unknown, FileNotFound, AccessDenied, TimeOut, ChaosMonkey }

.. then it's possible that an entity serialised by a process that has the v2 assembly loaded will include a **FailureReason** value that could not be deserialised into one of the named enum values where the v1 assembly is loaded. This would not result in an error, in the same way that this would not result in an error:

	var cause = (FailureReason)101;

However, you may find this behaviour undesirable and would prefer to map any unknown enum values onto the default value for that enum. That could be achieved by using a Deserialisation Type Converter, such as this:

	public class InvalidEnumToDefaultValueTypeConverter : IDeserialisationTypeConverter
	{
		public object ConvertIfRequired(Type targetType, object value)
		{
			if ((value == null) || !targetType.IsEnum)
				return value;

			if (Enum.IsDefined(targetType, value))
				return value;

			// Get default enum value
			return Activator.CreateInstance(targetType);
		}
	}

The deserialisation methods have overloads that allow any type converters to be specified - eg.

    var result = BinarySerialisation.Deserialise<PersonDetails>(
		serialisedData,
		new[] { new InvalidEnumToDefaultValueTypeConverter() }
	);
	
and
    
    var result =
		(new BinarySerialisationReader(
			stream,
			new[] { new InvalidEnumToDefaultValueTypeConverter() }
		)).Read<PersonDetails>();

One of the downsides to serialising the underlying value for enums is that reordering the enum names will change the values (unless each name is explicitly given a value). In this scenario:

	// v1 assembly has this enum
	enum Days { Mon, Tue, Wed, Thu, Fri, Sat, Sun }
	
	// v2 assembly has this enum (someone loves alphabetical ordering)
	enum Days { Fri, Mon, Sat, Sun, Tue, Thu, Wed }

.. if "Fri" is serialised by a process that has v2 assembly loaded and then deserialised as the v1 entity then it be interpeted as "Mon" and confusion could ensue.

One way to avoid this problem is to never reorder enum values (if new values are required then they would always be added to the end). Another way is to explicitly set the underlying values - eg.

	// v1 assembly has this enum
	enum Days { Mon = 0, Tue = 1, Wed = 2, Thu = 3, Fri = 4, Sat = 5, Sun = 6 }
	
	// v2 assembly has this enum (someone loves alphabetical ordering)
	enum Days { Fri = 4, Mon = 0, Sat = 5, Sun = 6, Tue = 1, Thu = 3, Wed = 2 }

A third way is to use serialisation and deserialisation type converters to serialise enums as their names, instead of they underlying values. Something like this:

	public class EnumAsStringTypeConverter
		: ISerialisationTypeConverter, IDeserialisationTypeConverter
	{
		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			if (value == null)
				return value;

			var valueType = value.GetType();
			if (!valueType.IsEnum)
				return value;

			return Enum.GetName(valueType, value);
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (!targetType.IsEnum || !(value is string valueString))
				return value;

			return Enum.TryParse(targetType, valueString, out var enumValue)
				? enumValue
				: Activator.CreateInstance(targetType);
		}
	}

Just as the deserialisation methods have overloads to specify type converters, so do the serialisation methods:

    var serialisedData = BinarySerialisation.Serialise(
		value,
		new[] { new EnumAsStringTypeConverter }
	);

and

    var writer = new BinarySerialisationWriter(stream);
    Serialiser.Instance.Serialise(
		value,
		new[] { new EnumAsStringTypeConverter },
		writer
	);