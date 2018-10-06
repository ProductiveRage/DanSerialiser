using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
		private static ConcurrentDictionary<Type, (ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>, IEnumerable<CachedNameData>)> _cache;
		static BinarySerialisationDeepCompiledMemberSetters()
		{
			_cache = new ConcurrentDictionary<Type, (ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>, IEnumerable<CachedNameData>)>();
		}

		/// <summary>
		/// This will explore the shape of the data that starts at the specified type - looking at its fields and properties and then looking at the fields and properties
		/// on those types, etc.. to try to produce as many member setters for those types as possible (the more member setters that it can produce, the less analysis work
		/// that the Serialiser should need to do). The dictionary that it returns may include null values that indicate that it is not possible to generate a member setter
		/// for that type (this can also save the Serialiser some work because it will know not to bother trying). The member setters will write data that uses Field Name
		/// Reference IDs, so the Serialiser will have to use the return CachedNameData list to write FieldNamePreLoad content ahead of the serialised object data.
		/// </summary>
		public static (ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>, IEnumerable<CachedNameData>) GetMemberSettersFor(Type serialisationTargetType)
		{
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			if (_cache.TryGetValue(serialisationTargetType, out var memberSetterData))
				return memberSetterData;

			var memberSetters = new Dictionary<Type, MemberSetterDetails>();
			var fieldNamesToDeclare = new List<CachedNameData>();
			GenerateMemberSettersForTypeIfPossible(serialisationTargetType, new HashSet<Type>(), memberSetters, fieldNamesToDeclare);
			var compiledMemberSetters = new ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>(
				memberSetters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.GetCompiledMemberSetter())
			);
			memberSetterData = (compiledMemberSetters, fieldNamesToDeclare);
			_cache.TryAdd(serialisationTargetType, memberSetterData);
			return memberSetterData;
		}

		private static void GenerateMemberSettersForTypeIfPossible(Type type, HashSet<Type> typesEncountered, Dictionary<Type, MemberSetterDetails> memberSetters, List<CachedNameData> fieldNamesToDeclare)
		{
			// Leave primitive-like values to the BinarySerialisationWriter's specialised methods (Boolean, String, DateTime, etc..)
			if (Serialiser.IsTreatedAsPrimitive(type))
				return;

			// Only consider consistent concrete types - sealed classes and structs, essentially. If a field has type MyClass and MyClass is a non-sealed class then
			// we might generate a member setter for MyClass but then encounter a MyDerivedClass at runtime that inherits from MyClass but has three extra fields;
			// those extra fields would not be serialised, then. If the field has a type that is a class that is sealed then we know that this can't happen and so
			// we're safe (and structs don't support inheritance, so we're safe with those as well).
			var isPredictableType = (type.IsClass && type.IsSealed) || type.IsValueType;
			if (!isPredictableType)
				return;

			if (typesEncountered.Contains(type))
				return;

			// We don't need additional member setters for array types because the BinarySerialisationCompiledMemberSetters can use a member setter for type A when
			// serialising instances of type A *and* when serialising arrays of A (it currently only tackle one-dimensional arrays but that could potentially change
			// in the future)
			if (type.IsArray)
			{
				GenerateMemberSettersForTypeIfPossible(type.GetElementType(), typesEncountered, memberSetters, fieldNamesToDeclare);
				return;
			}

			// In order to generate a member setter for Type A, we might need member setters for Types B and C and for them we might need member setters for Types
			// D and E, so we need to dig down to the bottom of the trees and work our way back up before we start trying to call TryToGenerateMemberSetter
			typesEncountered.Add(type);
			var (fields, properties) = DefaultTypeAnalyser.Instance.GetFieldsAndProperties(type);
			foreach (var field in fields.Where(f => GetFieldNameBytesIfWantoSerialiseField(f.Member, type) != null))
				GenerateMemberSettersForTypeIfPossible(field.Member.FieldType, typesEncountered, memberSetters, fieldNamesToDeclare);
			foreach (var property in properties.Where(p => GetFieldNameBytesIfWantoSerialiseProperty(p.Member) != null))
				GenerateMemberSettersForTypeIfPossible(property.Member.PropertyType, typesEncountered, memberSetters, fieldNamesToDeclare);

			// Now that we've been all the way down the chain for the current type, see if we can generate a member setter for it
			var memberSetterDetails = TryToGenerateMemberSetter(
				type,
				DefaultTypeAnalyser.Instance,
				t => memberSetters.TryGetValue(t, out var valueWriter) ? valueWriter?.MemberSetter : null
			);
			if (memberSetterDetails != null)
			{
				fieldNamesToDeclare.AddRange(memberSetterDetails.FieldsSet);
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
	}
}