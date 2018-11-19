using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
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
	/// A further restrictions to using this class is that the DefaultTypeAnalyser must be used (no other IAnalyseTypesForSerialisation implementation because other implementations
	/// may use different logic that means that the member setters here couldn't be shared between requests).
	/// </summary>
	internal static class BinarySerialisationDeepCompiledMemberSetters
	{
		/// <summary>
		/// This will explore the shape of the data that starts at the specified type - looking at its fields and properties and then looking at the fields and properties
		/// on those types, etc.. to try to produce as many member setters for those types as possible (the more member setters that it can produce, the less analysis work
		/// that the Serialiser should need to do). The dictionary that it returns may include null values that indicate that it is not possible to generate a member setter
		/// for that type (this can also save the Serialiser some work because it will know not to bother trying). The member setters will write data that uses Field Name
		/// Reference IDs, so the Serialiser will have to use the return CachedNameData list to write FieldNamePreLoad content ahead of the serialised object data.
		/// </summary>
		public static DeepCompiledMemberSettersGenerationResults GetMemberSettersFor(
			Type serialisationTargetType,
			IFastSerialisationTypeConverter[] typeConverters,
			ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> cache)
		{
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));
			if (cache == null)
				throw new ArgumentNullException(nameof(cache));

			if (cache.TryGetValue(serialisationTargetType, out var memberSetterData))
				return memberSetterData;

			var memberSetters = new Dictionary<Type, MemberSetterDetails>();
			var typeNamesToDeclare = new HashSet<CachedNameData>(CachedNameDataEqualityComparer.Instance);
			var fieldNamesToDeclare = new HashSet<CachedNameData>(CachedNameDataEqualityComparer.Instance);
			GenerateMemberSettersForTypeIfPossible(serialisationTargetType, new HashSet<Type>(), typeNamesToDeclare, fieldNamesToDeclare, memberSetters, typeConverters);
			var compiledMemberSetters = new ReadOnlyDictionary<Type, Action<object, BinarySerialisationWriter>>(
				memberSetters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.GetCompiledMemberSetter())
			);
			return cache.GetOrAdd(serialisationTargetType, new DeepCompiledMemberSettersGenerationResults(typeNamesToDeclare, fieldNamesToDeclare, compiledMemberSetters));
		}

		private static void GenerateMemberSettersForTypeIfPossible(
			Type type,
			HashSet<Type> typesEncountered,
			HashSet<CachedNameData> typeNamesToDeclare,
			HashSet<CachedNameData> fieldNamesToDeclare,
			Dictionary<Type, MemberSetterDetails> memberSetters,
			IFastSerialisationTypeConverter[] typeConverters)
		{
			// Leave primitive-like values to the BinarySerialisationWriter's specialised methods (Boolean, String, DateTime, etc..)
			if (Serialiser.IsTreatedAsPrimitive(type))
				return;

			// Ordinarily, we want to only consider consistent concrete types (sealed classes and structs, essentially) - if a field has type MyClass and MyClass is a
			// non -sealed class then we might generate a member setter for MyClass but then encounter a MyDerivedClass at runtime that inherits from MyClass but has
			// three extra fields; those extra fields would not be serialised, then. If the field has a type that is a class that is sealed then we know that this
			// can't happen and so we're safe (and structs don't support inheritance, so we're safe with those as well). 
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
				GenerateMemberSettersForTypeIfPossible(type.GetElementType(), typesEncountered, typeNamesToDeclare, fieldNamesToDeclare, memberSetters, typeConverters);
				return;
			}

			// Check the type converters here - if any of them would change the current type then we need to try to generate a member setter for the converted-to type
			// (rather than the current type). This check is done after the IsArray check above because the Serialiser has special handling for arrays that occurs before
			// handling for objects and so it's not possible to have a type converter than changes an array of T into something else.
			var (_, convertedToType) = TryToGetValueWriterViaTypeConverters(type);
			if (convertedToType != type)
			{
				typesEncountered.Add(type);
				type = convertedToType;
			}

			// In order to generate a member setter for Type A, we might need member setters for Types B and C and for them we might need member setters for Types
			// D and E, so we need to dig down to the bottom of the trees and work our way back up before we start trying to call TryToGenerateMemberSetter
			typesEncountered.Add(type);
			var (fields, properties) = DefaultTypeAnalyser.Instance.GetFieldsAndProperties(type);
			foreach (var field in fields.Where(f => GetFieldNameBytesIfWantoSerialiseField(f.Member, type) != null))
				GenerateMemberSettersForTypeIfPossible(field.Member.FieldType, typesEncountered, typeNamesToDeclare, fieldNamesToDeclare, memberSetters, typeConverters);
			foreach (var property in properties.Where(p => GetFieldNameBytesIfWantoSerialiseProperty(p.Member) != null))
				GenerateMemberSettersForTypeIfPossible(property.Member.PropertyType, typesEncountered, typeNamesToDeclare, fieldNamesToDeclare, memberSetters, typeConverters);

			var memberSetterDetails = TryToGenerateMemberSetter(
				type,
				DefaultTypeAnalyser.Instance,
				t =>
				{
					// Give the type converters a shot - if TryToGetValueWriterViaTypeConverters returns a non-null reference then one of them wants to control
					// the serialisation of this type (if null is returned then continue on to process as normal)
					var (valueWriterViaTypeConverter, _) = TryToGetValueWriterViaTypeConverters(t);
					if (valueWriterViaTypeConverter != null)
						return valueWriterViaTypeConverter;

					if (memberSetters.TryGetValue(t, out var valueWriter) && (valueWriter != null))
					{
						// The TryToGenerateMemberSetter method has requested a member setter for one of the fields or properties and we have access to a member
						// setter that matches that type precisely - this is the ideal case!
						return ValueWriter.PopulateValue(valueWriter.MemberSetter);
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

			(ValueWriter, Type) TryToGetValueWriterViaTypeConverters(Type t)
			{
				foreach (var typeConverter in typeConverters)
				{
					var typeNames = new List<CachedNameData>();
					var fieldNames = new List<CachedNameData>();
					var typeConverterWriter = typeConverter.GetDirectWriterIfPossible(
						t,
						requestedMemberSetterType =>
						{
							var requestedMemberSetterTypeWriter = TryToGenerateMemberSetter(
								requestedMemberSetterType,
								DefaultTypeAnalyser.Instance,
								nestedType => (memberSetters.TryGetValue(nestedType, out var valueWriter) && (valueWriter != null))
									? ValueWriter.PopulateValue(valueWriter.MemberSetter)
									: null
							);
							if (requestedMemberSetterTypeWriter != null)
							{
								typeNames.Add(requestedMemberSetterTypeWriter.TypeName);
								fieldNames.AddRange(requestedMemberSetterTypeWriter.FieldsSet);
							}
							return requestedMemberSetterTypeWriter;
						}
					);
					if (typeConverterWriter != null)
					{
						if (typeConverterWriter.MemberSetterIfNotSettingToDefault == null)
							return (ValueWriter.SetValueToDefault, typeConverterWriter.ConvertedToType);

						var newTypeName = GetTypeNameBytes(typeConverterWriter.ConvertedToType);
						typeNamesToDeclare.Add(newTypeName);
						foreach (var typeName in typeNames)
							typeNamesToDeclare.Add(typeName);
						foreach (var fieldName in fieldNames)
							fieldNamesToDeclare.Add(fieldName);
						return (ValueWriter.OverrideValue(newTypeName, typeConverterWriter.MemberSetterIfNotSettingToDefault), typeConverterWriter.ConvertedToType);
					}
				}
				return (null, t);
			}
		}

		/// <summary>
		/// A type converter may potentially request member setters for multiple types and so there may be type names and field names from multiple types that are required
		/// in order to serialise the value (not just the type name and field names of the type that the converter changes the value into) - this class contains all of that
		/// information.
		/// </summary>
		private sealed class FastTypeConverterSerialisationDependencies
		{
			public FastTypeConverterSerialisationDependencies(CachedNameData newTypeName, IEnumerable<CachedNameData> otherTypeNames, IEnumerable<CachedNameData> fieldNames, LambdaExpression memberSetter)
			{
				NewTypeName = newTypeName ?? throw new ArgumentNullException(nameof(newTypeName));
				OtherTypeNames = otherTypeNames ?? throw new ArgumentNullException(nameof(otherTypeNames));
				FieldNames = fieldNames ?? throw new ArgumentNullException(nameof(fieldNames));
				MemberSetter = memberSetter ?? throw new ArgumentNullException(nameof(memberSetter));
			}
			public CachedNameData NewTypeName { get; }
			public IEnumerable<CachedNameData> OtherTypeNames { get; }
			public IEnumerable<CachedNameData> FieldNames { get; }
			public LambdaExpression MemberSetter { get; }
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