using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using DanSerialiser.Reflection;
using static DanSerialiser.CachedLookups.BinarySerialisationCompiledMemberSetters;
using static DanSerialiser.CachedLookups.BinarySerialisationWriterCachedNames;

namespace DanSerialiser.CachedLookups
{
	/// <summary>
	/// The BinarySerialisationCompiledMemberSetters class is intended to optimise the serialisation of 'leaf node' types - types that are at the bottom of a tree and whose fields
	/// and properties are all known to be types that do not need require reference-tracking because they will never be part of a cicular reference loop and they will never be
	/// references may appear at multiple points in the data. Those types may be serialised using compiled LINQ expression that push the content directly to the serialisation
	/// writer and bypass any reference tracking that may be needed elsewhere. This is effective but it limits what types it applies to.
	/// 
	/// If we're in a scenario where raw speed is most important then reference tracking may be disabled entirely and then the optimisation could be applied to more types - it's
	/// crucial to note, though, that having no reference tracking means that any circular references in the source data will result in a stack overflow and any references that
	/// appear multiple times in the source data will have content repeated in the serialisation data (when reference-tracking is enabled, these problems may be avoided - the
	/// NoReferenceReuse option will detect circular references and throw an exception that may be caught if any circular reference loops are encountered while the
	/// SupportReferenceReUseInMostlyTreeLikeStructure option uses Reference IDs so that if the same reference appears multiple times in the source data then it will only be
	/// serialised once and then a 'pointer' will be included in the data so that the references are reconstructed to match the source data when deserialisation occurs).
	/// 
	/// If reference tracking CAN be skipped then member setters for the source data may be pre-generated before the serialisation process begins and then much more of the data
	/// should get written using the optimised writers. This class will perform that pre-generation work.
	/// 
	/// Member setters that are generated here are cached for the life time of the application. The assumption is that an application is going to deal with serialising and de-
	/// serialising the same types throughout its life and reusing the optimised member setters seems optimal when performance is the primary concern (which it presumably is
	/// if reference tracking is disabled).
	/// 
	/// Further restrictions to using this class are that no de/serialisation type converters may be used because they could change the shape of the data in ways that are not
	/// knowable until they are called (and this analysis needs to happen before that) and that the DefaultTypeAnalyser must be used (no other IAnalyseTypesForSerialisation
	/// implementation because other implementations may use different logic that means that the member setters here couldn't be shared between requests).
	/// </summary>
	internal static class BinarySerialisationDeepCompiledMemberSetters
	{
		private static ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> _cache;
		static BinarySerialisationDeepCompiledMemberSetters()
		{
			_cache = new ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults>();
		}

		/// <summary>
		/// This will explore the shape of the data that starts at the specified type - looking at its fields and properties and then looking at the fields and properties
		/// on those types, etc.. to try to produce as many member setters for those types as possible (the more member setters that it can produce, the less analysis work
		/// that the Serialiser should need to do). The dictionary that it returns may include null values that indicate that it is not possible to generate a member setter
		/// for that type (this can also save the Serialiser some work because it will know not to bother trying). The member setters will write data that uses Field Name
		/// Reference IDs, so the Serialiser will have to use the return CachedNameData list to write FieldNamePreLoad content ahead of the serialised object data.
		/// </summary>
		public static DeepCompiledMemberSettersGenerationResults GetMemberSettersFor(Type serialisationTargetType)
		{
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			if (_cache.TryGetValue(serialisationTargetType, out var memberSetterData))
				return memberSetterData;

			var memberSetters = new Dictionary<Type, MemberSetterDetails>();
			var typeNamesToDeclare = new HashSet<CachedNameData>(CachedNameDataEqualityComparer.Instance);
			var fieldNamesToDeclare = new HashSet<CachedNameData>(CachedNameDataEqualityComparer.Instance);
			GenerateMemberSettersForTypeIfPossible(serialisationTargetType, new HashSet<Type>(), typeNamesToDeclare, fieldNamesToDeclare, memberSetters, fieldIsHappyToIgnoreSpecialisations: false);
			var compiledMemberSetters = new ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>(
				memberSetters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.GetCompiledMemberSetter())
			);
			memberSetterData = new DeepCompiledMemberSettersGenerationResults(typeNamesToDeclare, fieldNamesToDeclare, compiledMemberSetters);
			_cache.TryAdd(serialisationTargetType, memberSetterData);
			return memberSetterData;
		}

		private static void GenerateMemberSettersForTypeIfPossible(
			Type type,
			HashSet<Type> typesEncountered,
			HashSet<CachedNameData> typeNamesToDeclare,
			HashSet<CachedNameData> fieldNamesToDeclare,
			Dictionary<Type, MemberSetterDetails> memberSetters,
			bool fieldIsHappyToIgnoreSpecialisations)
		{
			// Leave primitive-like values to the BinarySerialisationWriter's specialised methods (Boolean, String, DateTime, etc..)
			if (Serialiser.IsTreatedAsPrimitive(type))
				return;

			// Ordinarily, we want to only consider consistent concrete types (sealed classes and structs, essentially) - if a field has type MyClass and MyClass is a
			// non -sealed class then we might generate a member setter for MyClass but then encounter a MyDerivedClass at runtime that inherits from MyClass but has
			// three extra fields; those extra fields would not be serialised, then. If the field has a type that is a class that is sealed then we know that this
			// can't happen and so we're safe (and structs don't support inheritance, so we're safe with those as well). 
			//
			// However, if we're looking at generating a member setter for a type specified on a field or property that has the [SpecialisationsMayBeIgnoredWhenSerialising]
			// attribute then we're willing to ignore the fact that there may be derived classes to consider. This means that if fieldIsHappyToIgnoreSpecialisations is true
			// then we'll try to create a member setter for the current type, even if it's a class that isn't sealed. There are still limits, though - we still can't generate
			// member setters for abstract classes or for interfaces because the deserialisation process needs to be given a type name that it can instantiate on the other
			// side and things will be bad if it is tries to do that for a type name relating to an abstract class or interface. For more information about why someone
			// might not care about losing information in the serialisation process that may only appear on specialised / derived types, see the comments on the
			// SpecialisationsMayBeIgnoredWhenSerialisingAttribute class.
			var isPredictableType = (type.IsClass && type.IsSealed) || type.IsValueType;
			if (!isPredictableType)
			{
				if (!fieldIsHappyToIgnoreSpecialisations || (type.IsClass && type.IsAbstract) || type.IsInterface)
					return;
			}
			if (typesEncountered.Contains(type))
				return;

			// We don't need additional member setters for array types because the BinarySerialisationCompiledMemberSetters can use a member setter for type A when
			// serialising instances of type A *and* when serialising arrays of A (it currently only tackle one-dimensional arrays but that could potentially change
			// in the future)
			if (type.IsArray)
			{
				GenerateMemberSettersForTypeIfPossible(type.GetElementType(), typesEncountered, typeNamesToDeclare, fieldNamesToDeclare, memberSetters, fieldIsHappyToIgnoreSpecialisations);
				return;
			}

			// In order to generate a member setter for Type A, we might need member setters for Types B and C and for them we might need member setters for Types
			// D and E, so we need to dig down to the bottom of the trees and work our way back up before we start trying to call TryToGenerateMemberSetter
			typesEncountered.Add(type);
			var (fields, properties) = DefaultTypeAnalyser.Instance.GetFieldsAndProperties(type);
			foreach (var field in fields.Where(f => GetFieldNameBytesIfWantoSerialiseField(f.Member, type) != null))
			{
				// The [SpecialisationsMayBeIgnoredWhenSerialising] attribute "latches" when it's set on a field or property - if a field has this attribute then we'll
				// proceed as is every field or property of that field's type had the attribute and do the same for every field or property that THOSE types have (this
				// is so that we can, for example, put it on a Dictionary<,> field and ignore the fact that there might be instances where the field's value is of a
				// type DERIVED from Dictionary<,> AND we can write null values for the IEqualityComparer field of the dictionary)
				GenerateMemberSettersForTypeIfPossible(
					field.Member.FieldType,
					typesEncountered,
					typeNamesToDeclare,
					fieldNamesToDeclare,
					memberSetters,
					fieldIsHappyToIgnoreSpecialisations ||
						(field.Member.GetCustomAttribute<SpecialisationsMayBeIgnoredWhenSerialisingAttribute>() != null) ||
						(BackingFieldHelpers.TryToGetPropertyRelatingToBackingField(field.Member)?.GetCustomAttribute<SpecialisationsMayBeIgnoredWhenSerialisingAttribute>() != null)
				);
			}
			foreach (var property in properties.Where(p => GetFieldNameBytesIfWantoSerialiseProperty(p.Member) != null))
			{
				// See notes in loop above about [SpecialisationsMayBeIgnoredWhenSerialising] behaviour
				GenerateMemberSettersForTypeIfPossible(
					property.Member.PropertyType,
					typesEncountered,
					typeNamesToDeclare,
					fieldNamesToDeclare,
					memberSetters,
					fieldIsHappyToIgnoreSpecialisations || (property.Member.GetCustomAttribute<SpecialisationsMayBeIgnoredWhenSerialisingAttribute>() != null)
				);
			}

			// Now that we've been all the way down the chain for the current type, see if we can generate a member setter for it
			var memberSetterDetails = TryToGenerateMemberSetter(
				type,
				DefaultTypeAnalyser.Instance,
				t =>
				{
					if (memberSetters.TryGetValue(t, out var valueWriter) && (valueWriter != null))
					{
						// The TryToGenerateMemberSetter method has requested a member setter for one of the fields or properties and we have access to a member
						// setter that matches that type precisely - this is the ideal case!
						return ValueWriter.PopulateValue(valueWriter.MemberSetter);
					}

					if (fieldIsHappyToIgnoreSpecialisations && ((t.IsClass && t.IsAbstract) || t.IsInterface))
					{
						// The current type is from a field or property annotated with [SpecialisationsMayBeIgnoredWhenSerialising] (or the attribute appeared
						// further up the chain) and that means that we're able to be more flexible with awkward types that require specialisation (types that
						// can't be instantiated directly and that only make sense when inherited or implemented by another class) - by "more flexible", I mean
						// that we can ignore them and write away a null value for it. An example use case for this would be a class that we want to serialise
						// as efficiently as possible using the SpeedyButLimited option, where that class has a field that is of type Dictionary<int, string>
						// and that field will ONLY EVER have a Dictionary<int, string> reference (and never be set to an instance of a type that is derived
						// from Dictionary<int, string>) and it will never have an equality comparer specified; in that case, the attribute may be added to
						// that field or property. In that example, if the attribute was't applied to the field / property then serialisation would still
						// succeed but it would be a little bit slower. If there is a chance that the field / property MIGHT be set to an instance of a
						// type that is derived from Dictionary<int, string> or if there is a chance that a different equality comparer might be needed
						// then the [SpecialisationsMayBeIgnoredWhenSerialising] attribute should NOT be applied to the field / property (again, this
						// will not stop the serialisation process from succeeding, it will just mean that it will be slower).
						return ValueWriter.SetValueToDefault;
					}

					return null;
				}
			);
			if (memberSetterDetails != null)
			{
				typeNamesToDeclare.Add(memberSetterDetails.TypeName);
				foreach (var fieldName in memberSetterDetails.FieldsSet)
					fieldNamesToDeclare.Add(fieldName);
				memberSetters.Add(type, memberSetterDetails);
			}
			else
			{
				// If we were unable to generate a member setter for this type then record the null value - it's useful for the GetMemberSettersFor method to be
				// able to return member setters for types that it could generate them for but it's also useful to make it clear what types it was NOT possible
				// to generate them for because it can save the caller some work (they won't waste time trying to generate a member setter themselves)
				memberSetters.Add(type, null);
			}
		}

		private sealed class CachedNameDataEqualityComparer : IEqualityComparer<CachedNameData>
		{
			public static CachedNameDataEqualityComparer Instance { get; } = new CachedNameDataEqualityComparer();
			private CachedNameDataEqualityComparer() { }

			public bool Equals(CachedNameData x, CachedNameData y) => x?.ID == y?.ID;
			public int GetHashCode(CachedNameData obj) => (obj == null) ? -1 : obj.ID;
		}

		public sealed class DeepCompiledMemberSettersGenerationResults
		{
			public DeepCompiledMemberSettersGenerationResults(
				IEnumerable<CachedNameData> typeNamesToDeclare,
				IEnumerable<CachedNameData> fieldNamesToDeclare,
				ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>> memberSetters)
			{
				TypeNamesToDeclare = typeNamesToDeclare;
				FieldNamesToDeclare = fieldNamesToDeclare;
				MemberSetters = memberSetters;
			}

			public IEnumerable<CachedNameData> TypeNamesToDeclare { get; }
			public IEnumerable<CachedNameData> FieldNamesToDeclare { get; }
			public ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>> MemberSetters { get; }
		}
	}
}