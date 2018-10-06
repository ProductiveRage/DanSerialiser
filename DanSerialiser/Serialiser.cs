using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DanSerialiser.CachedLookups;
using DanSerialiser.Exceptions;
using DanSerialiser.Reflection;
using DanSerialiser.TypeConverters;

namespace DanSerialiser
{
	public sealed class Serialiser
	{
		public static Serialiser Instance { get; } = new Serialiser(DefaultTypeAnalyser.Instance);

		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		internal Serialiser(IAnalyseTypesForSerialisation typeAnalyser) // internal constructor is intended for unit testing only
		{
			_typeAnalyser = typeAnalyser ?? throw new ArgumentNullException(nameof(typeAnalyser));
		}

		public void Serialise<T>(T value, IWrite writer) => Serialise(value, new ISerialisationTypeConverter[0], writer);

		public void Serialise<T>(T value, ISerialisationTypeConverter[] typeConverters, IWrite writer)
		{
			if (writer == null)
				throw new ArgumentNullException(nameof(writer));
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));
			if (typeConverters.Any(t => t == null))
				throw new ArgumentException("Null reference encountered in " + nameof(typeConverters));

			Stack<object> parentsIfReferenceReuseDisallowed;
			Dictionary<object, int> objectHistoryIfReferenceReuseAllowed;
			HashSet<int> deferredInitialisationObjectReferenceIDsIfSupported;
			if (writer.ReferenceReuseStrategy == ReferenceReuseOptions.NoReferenceReuse)
			{
				parentsIfReferenceReuseDisallowed = new Stack<object>();
				objectHistoryIfReferenceReuseAllowed = null;
				deferredInitialisationObjectReferenceIDsIfSupported = null;
			}
			else if (writer.ReferenceReuseStrategy == ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure)
			{
				parentsIfReferenceReuseDisallowed = null;
				objectHistoryIfReferenceReuseAllowed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
				deferredInitialisationObjectReferenceIDsIfSupported = null;
			}
			else if (writer.ReferenceReuseStrategy == ReferenceReuseOptions.OptimiseForWideCircularReferences)
			{
				parentsIfReferenceReuseDisallowed = null;
				objectHistoryIfReferenceReuseAllowed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
				deferredInitialisationObjectReferenceIDsIfSupported = new HashSet<int>();
			}
			else if (writer.ReferenceReuseStrategy == ReferenceReuseOptions.SpeedyButLimited)
			{
				parentsIfReferenceReuseDisallowed = null;
				objectHistoryIfReferenceReuseAllowed = null;
				deferredInitialisationObjectReferenceIDsIfSupported = null;
			}
			else
				throw new NotSupportedException($"{nameof(writer)} has unsupported {nameof(writer.ReferenceReuseStrategy)}: {writer.ReferenceReuseStrategy}");

			// Let the writer to any upfront analysis that it wants to - this will return a "generatedMemberSetters" dictionary and so we may not have to
			// start from an empty member setter dictionary every time
			var type = value?.GetType() ?? typeof(T);
			var generatedMemberSetters = writer.PrepareForSerialisation(type, typeConverters);

			// We need to know the type that we're serialising and that's why there is a generic type param, so that the caller HAS to specify one even if
			// they're passing null. If we don't have null then take the type from the value argument, otherwise use the type param (we should prefer the
			// value's type because it may be more specific - eg. could call this with a T of object and a value that is a string, in which case we want
			// to process it as a string and not an object).
			Serialise(
				value,
				type,
				false, // populatingDeferredObject is always false at the top level, it may become true in some nested calls, depending upon configuration
				parentsIfReferenceReuseDisallowed,
				objectHistoryIfReferenceReuseAllowed,
				deferredInitialisationObjectReferenceIDsIfSupported,
				generatedMemberSetters,
				typeConverters: typeConverters,
				writer: writer
			);
		}

		private void Serialise(
			object value,
			Type type,
			bool populatingDeferredObject,
			Stack<object> parentsIfReferenceReuseDisallowed,
			Dictionary<object, int> objectHistoryIfReferenceReuseAllowed,
			HashSet<int> deferredInitialisationObjectReferenceIDsIfSupported,
			Dictionary<Type, Action<object>> generatedMemberSetters,
			ISerialisationTypeConverter[] typeConverters,
			IWrite writer)
		{
			if ((parentsIfReferenceReuseDisallowed != null) && parentsIfReferenceReuseDisallowed.Contains(value, ReferenceEqualityComparer.Instance))
				throw new CircularReferenceException();

			// Give the type converters a crack at the current value - if any of them change the value then take that as the new value and don't consider any other converters
			foreach (var typeConverter in typeConverters)
			{
				var updatedValue = typeConverter.ConvertIfRequired(value);
				if (!ReferenceEquals(updatedValue, value))
				{
					value = updatedValue;
					type = value?.GetType() ?? typeof(object);
					break;
				}
			}

			// If we've got a Nullable<> then unpack the internal value/type - if it's null then we'll get a null ObjectStart/ObjectEnd value which the BinarySerialisationReader will
			// happily interpret (reading it as a null and setting the Nullable<> field) and if it's non-null then we'll serialise just the value itself (again, the reader will take
			// that value and happily set a Nullable<> field - so if we write an int then the reader will read the int and set the int? field just fine). Doing this means that we
			// have less work to do (otherwise we'd record the Nullable<> as an object and write the type name and so it's just more work to write, more work to read and takes
			// up more data in the serialisation output).
			if ((value != null) && type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
				type = type.GetGenericArguments()[0];

			if (type == CommonTypeOfs.Boolean)
			{
				writer.Boolean((Boolean)value);
				return;
			}
			if (type == CommonTypeOfs.Byte)
			{
				writer.Byte((Byte)value);
				return;
			}
			if (type == CommonTypeOfs.SByte)
			{
				writer.SByte((SByte)value);
				return;
			}

			if (type == CommonTypeOfs.Int16)
			{
				writer.Int16((Int16)value);
				return;
			}
			if (type == CommonTypeOfs.Int32)
			{
				writer.Int32((Int32)value);
				return;
			}
			if (type == CommonTypeOfs.Int64)
			{
				writer.Int64((Int64)value);
				return;
			}

			if (type == CommonTypeOfs.UInt16)
			{
				writer.UInt16((UInt16)value);
				return;
			}
			if (type == CommonTypeOfs.UInt32)
			{
				writer.UInt32((UInt32)value);
				return;
			}
			if (type == CommonTypeOfs.UInt64)
			{
				writer.UInt64((UInt64)value);
				return;
			}

			if (type == CommonTypeOfs.Single)
			{
				writer.Single((Single)value);
				return;
			}
			if (type == CommonTypeOfs.Double)
			{
				writer.Double((Double)value);
				return;
			}
			if (type == CommonTypeOfs.Decimal)
			{
				writer.Decimal((Decimal)value);
				return;
			}

			if (type == CommonTypeOfs.Char)
			{
				writer.Char((Char)value);
				return;
			}
			if (type == CommonTypeOfs.String)
			{
				if (value == null)
					writer.Null();
				else
					writer.String((String)value);
				return;
			}

			if (type == CommonTypeOfs.DateTime)
			{
				writer.DateTime((DateTime)value);
				return;
			}
			if (type == CommonTypeOfs.TimeSpan)
			{
				writer.TimeSpan((TimeSpan)value);
				return;
			}

			if (type == CommonTypeOfs.Guid)
			{
				writer.Guid((Guid)value);
				return;
			}

			// For Object and Array types, if we've got a null reference then write a Null value (having this null check here avoids the type.IsEnum and type.IsArray checks
			// for cases where we DO have a null reference(
			if (value == null)
			{
				writer.Null();
				return;
			}

			if (type.IsEnum)
			{
				Serialise(
					value,
					type.GetEnumUnderlyingType(),
					false, // populatingDeferredObject is never applicable to enum values as they are not reference types
					parentsIfReferenceReuseDisallowed,
					objectHistoryIfReferenceReuseAllowed,
					deferredInitialisationObjectReferenceIDsIfSupported,
					generatedMemberSetters,
					typeConverters,
					writer
				);
				return;
			}

			if (type.IsArray)
			{
				// TODO: Need to ensure that de/serialising arrays with multiple dimensions works!
				var elementType = type.GetElementType();
				writer.ArrayStart(value, elementType);
				var array = (Array)value;
				Dictionary<int, object> newObjectReferencesFromArray;
				HashSet<int> deferredInitialisationObjectReferenceIDs;
				if (deferredInitialisationObjectReferenceIDsIfSupported != null)
				{
					// If deferredInitialisationObjectReferenceIDsIfSupported is non-null then it means that we're operating in an optimise-for-wide-circular-references mode
					// and we want to take a breadth-first approach to serialising arrays - we want to build up a list of all object that are array elements and we'll put
					// them in a special "delayed content-population" / "deferred initialisation" list and then, when the array data is serialised, instead of us diving
					// deep into each object to write out its contents, we'll just include the Object Reference ID to one of these deferred-initialisation objects. After
					// the array has been serialised, we'll write out the information that allows these not-yet-populated object references to have all of their fields
					// set. I'm finding it difficult to describe this well so I'll go with an example - the source data is an array of items A1, A2, A3 and each of these
					// items has a property that is an array that contains B1, B2, B3 (all of A1, A2, A3 reference the same B1, B2, B3 in an array property). All of these
					// B1, B2, B3 items have a property that is an array that includes references to A1, A2, A3 and so there are circular references throughout. If this was
					// approach like a tree structure then we would start serialising A1 and we'd encounter its array property and so we'd start serialising B1 and then ITS
					// array property would include A1 (which we'd include an Object Reference ID for because we've already encountered A1) but we'd then have to serialise
					// A2 and A2 would lead to B2, etc.. and each of these jumps would be another Serialise call and the call stack would grow and grow and grow. If we use
					// THIS approach (instead of treating it as a tree) then we start with the array A1, A2, A3 and we create Object References for them and then start to
					// serialise A1 - this leads to A1's array of B1, B2, B3 and we create Object References for them and then serialise each of them; when they reference
					// A1, A2, A3 we will be able to record Object Reference IDs because we've already encountered A1, A2, A3. This way, the call stack is kept much more
					// shallow (when there are only 3-element arrays, there isn't any real problem but if the arrays have 1000s of elements then a stack overflow exception
					// is likely to occur if the data is serialised as a tree). The disadvantage to this approach is that it is less intuitive and it involves defining
					// objects in two passes (first to say "here is an object reference which currently is missing data" and then a second to say "here is the data for that
					// earlier object") - this affects both the serialisation process AND the deserialisation process. There are also some optimisations which the reader
					// can't make if the object creation / population is spread out (but taking that hit is might be the only option if you have an object model that will
					// cause a stack overflow if you don't!)
					newObjectReferencesFromArray = new Dictionary<int, object>();
					if (!IsTreatedAsPrimitive(elementType))
					{
						for (var i = 0; i < array.Length; i++)
						{
							// Note: We want to identify the objects that are referenced DIRECTLY by this array element (we don't want to try to follow the whole chain or
							// we run the risk of a stack overflow again). However, if the element is a struct then we may need to walk down its members until we do find
							// the object references (eg. if we have an array of KeyValuePair<TKey, TEntry> then find the object references by looking at the Key and
							// Value properties of the KeyValuePair struct) - this is handled by the PrepareArrayObjectReferences method.
							var element = array.GetValue(i);
							PrepareArrayObjectReferences(element, elementType, objectHistoryIfReferenceReuseAllowed, newObjectReferencesFromArray, writer);
						}
					}
					if (newObjectReferencesFromArray.Any())
					{
						deferredInitialisationObjectReferenceIDs = new HashSet<int>(deferredInitialisationObjectReferenceIDsIfSupported);
						deferredInitialisationObjectReferenceIDs.UnionWith(newObjectReferencesFromArray.Keys);
					}
					else
						deferredInitialisationObjectReferenceIDs = deferredInitialisationObjectReferenceIDsIfSupported;
				}
				else
				{
					newObjectReferencesFromArray = null;
					deferredInitialisationObjectReferenceIDs = null;
				}
				if (parentsIfReferenceReuseDisallowed != null)
					parentsIfReferenceReuseDisallowed.Push(value);
				for (var i = 0; i < array.Length; i++)
				{
					var element = array.GetValue(i);
					Serialise(
						element,
						elementType,
						false, // populatingDeferredObject should only ever be true in the Serialise call below
						parentsIfReferenceReuseDisallowed,
						objectHistoryIfReferenceReuseAllowed,
						deferredInitialisationObjectReferenceIDs,
						generatedMemberSetters,
						typeConverters,
						writer
					);
				}
				if (parentsIfReferenceReuseDisallowed != null)
					parentsIfReferenceReuseDisallowed.Pop();
				if (newObjectReferencesFromArray != null)
				{
					foreach (var deferredObject in newObjectReferencesFromArray.Values)
					{
						Serialise(
							deferredObject,
							deferredObject.GetType(),
							true, // Pass true for populatingDeferredObject to indicate that we need to record data that will used to populate an delayed-content-population reference
							parentsIfReferenceReuseDisallowed,
							objectHistoryIfReferenceReuseAllowed,
							deferredInitialisationObjectReferenceIDsIfSupported,
							generatedMemberSetters,
							typeConverters,
							writer
						);
					}
				}
				writer.ArrayEnd();
				return;
			}

			writer.ObjectStart(value);
			bool recordedAsOtherReference, recordedAsPostponedReference;
			if ((objectHistoryIfReferenceReuseAllowed != null) && IsApplicableToObjectHistory(type))
			{
				if (objectHistoryIfReferenceReuseAllowed.TryGetValue(value, out int referenceID))
				{
					recordedAsOtherReference = true;
					recordedAsPostponedReference = (deferredInitialisationObjectReferenceIDsIfSupported != null) && deferredInitialisationObjectReferenceIDsIfSupported.Contains(referenceID);
				}
				else
				{
					if (objectHistoryIfReferenceReuseAllowed.Count == BinaryReaderWriterShared.MaxReferenceCount)
					{
						// The references need to be tracked in the object history dictionary and there is a limit to how many items will fit (MaxReferenceCount will be int.MaxValue) -
						// this probably won't ever be hit (more likely to run out of memory first) but it's better to have a descriptive exception in case it ever is encountered
						throw new MaxObjectGraphSizeExceededException();
					}
					referenceID = objectHistoryIfReferenceReuseAllowed.Count;
					objectHistoryIfReferenceReuseAllowed[value] = referenceID;
					recordedAsOtherReference = false;
					recordedAsPostponedReference = false;
				}
				writer.ReferenceId(referenceID);
			}
			else
			{
				recordedAsOtherReference = false;
				recordedAsPostponedReference = false;
			}
			if (recordedAsPostponedReference)
				writer.ObjectContentPostponed();
			else if (!recordedAsOtherReference || populatingDeferredObject)
			{
				// If this object is associated with an Object Reference ID that has been encountered before then we won't to repeat the information for the fields on that object
				// (because this would have been written the first time that the object was met). However, if the first time that the object was declared was for deferred
				// initialisation (which is used in OptimiseForWideCircularReferences configurations) then this might be the time that the content for that object needs
				// to be recorded - in which case, the populatingDeferredObject will be true.
				SerialiseObjectFieldsAndProperties(
					value,
					parentsIfReferenceReuseDisallowed,
					objectHistoryIfReferenceReuseAllowed,
					deferredInitialisationObjectReferenceIDsIfSupported,
					generatedMemberSetters,
					typeConverters,
					writer
				);
			}
			writer.ObjectEnd();
		}

		private static bool IsApplicableToObjectHistory(Type type)
		{
			return (type != CommonTypeOfs.String) && !type.IsValueType;
		}

		private void SerialiseObjectFieldsAndProperties(
			object value,
			Stack<object> parentsIfReferenceReuseDisallowed,
			Dictionary<object, int> objectHistoryIfReferenceReuseAllowed,
			HashSet<int> deferredInitialisationObjectReferenceIDsIfSupported,
			Dictionary<Type, Action<object>> generatedMemberSetters,
			ISerialisationTypeConverter[] typeConverters,
			IWrite writer)
		{
			// It may be possible for a "type generator" to be created for some types (generally simple types that won't require any nested Serialise calls that involve tracking
			// parentsIfReferenceReuseDisallowed or objectHistoryIfReferenceReuseAllowed), so check that first. There are three cases; 1. we don't have any type generator data
			// about the current type, 2. we have tried to retrieve a type generator before and got back null (meaning that this type does not match the writer's conditions
			// for being able to create a type generator) and 3. we have successfully created a type generator before. If it's case 3 then we'll use that type generator
			// instead of enumerating fields below but if it's case 1 or 2 then we'll have to do that work (but if it's case 1 then we'll try to find out whether it's
			// possible to create a type generator at the bottom of this method).
			var valueType = value.GetType();
			var haveTriedToGenerateMemberSetterBefore = generatedMemberSetters.TryGetValue(valueType, out var memberSetter);
			if (haveTriedToGenerateMemberSetterBefore && (memberSetter != null))
			{
				memberSetter(value);
				return;
			}

			// Write out all of the data for the value
			var (fields, properties) = _typeAnalyser.GetFieldsAndProperties(valueType);
			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				if (writer.FieldName(field.Member, valueType))
				{
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Push(value);
					var fieldValue = field.Reader(value);
					Serialise(
						fieldValue,
						fieldValue?.GetType() ?? field.Member.FieldType,
						false, // populatingDeferredObject can only refer to the current referene and would never be propagated to member references
						parentsIfReferenceReuseDisallowed,
						objectHistoryIfReferenceReuseAllowed,
						deferredInitialisationObjectReferenceIDsIfSupported,
						generatedMemberSetters,
						typeConverters,
						writer
					);
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Pop();
				}
			}
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				if (writer.PropertyName(property.Member, valueType))
				{
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Push(value);
					var propertyValue = property.Reader(value);
					Serialise(
						propertyValue,
						propertyValue?.GetType() ?? property.Member.PropertyType,
						false, // populatingDeferredObject can only refer to the current referene and would never be propagated to member references
						parentsIfReferenceReuseDisallowed,
						objectHistoryIfReferenceReuseAllowed,
						deferredInitialisationObjectReferenceIDsIfSupported,
						generatedMemberSetters,
						typeConverters,
						writer
					);
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Pop();
				}
			}

			// If we have tried before to create a type generator for this type and were unsuccessful then there is nothing more to do..
			if (haveTriedToGenerateMemberSetterBefore)
				return;

			// .. but if we HAVEN'T tried to create a type generator before then ask the writer if it's able to do so (this is done after the first time that an instance of
			// the type has been fully serialised so that the writer has a chance to create any Name Reference IDs that it might want to use for the member names and potentially
			// have done some other forms of caching)
			generatedMemberSetters[valueType] = writer.TryToGenerateMemberSetter(valueType);
		}

		private void PrepareArrayObjectReferences(object element, Type elementType, Dictionary<object, int> objectHistoryIfReferenceReuseAllowed, Dictionary<int, object> newObjectReferencesFromArray, IWrite writer)
		{
			if ((element == null) || IsTreatedAsPrimitive(elementType))
				return;

			// If this is an object reference then add it to the dictionaries and stop looking. We need to keep digging if it's a struct in case there are any more object references to
			// find - we don't create struct "references" while deserialising since structs are passsed around by-copy whereas reference types are passed by-reference (if we're serialising
			// an array of KeyValuePair then we'll want to get the object references for the key and value, if they ARE reference types)
			if (IsApplicableToObjectHistory(elementType))
			{
				if (!objectHistoryIfReferenceReuseAllowed.ContainsKey(element))
				{
					if (objectHistoryIfReferenceReuseAllowed.Count == BinaryReaderWriterShared.MaxReferenceCount)
					{
						// The references need to be tracked in the object history dictionary and there is a limit to how many items will fit (MaxReferenceCount will be int.MaxValue) -
						// this probably won't ever be hit (more likely to run out of memory first) but it's better to have a descriptive exception in case it ever is encountered
						throw new MaxObjectGraphSizeExceededException();
					}
					var referenceID = objectHistoryIfReferenceReuseAllowed.Count;
					objectHistoryIfReferenceReuseAllowed[element] = referenceID;
					newObjectReferencesFromArray[referenceID] = element;
				}
				return;
			}

			// Write out all of the data for the value
			var (fields, properties) = _typeAnalyser.GetFieldsAndProperties(elementType);
			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				if (writer.ShouldSerialiseField(field.Member, elementType))
					PrepareArrayObjectReferences(field.Reader(element), field.Member.FieldType, objectHistoryIfReferenceReuseAllowed, newObjectReferencesFromArray, writer);
			}
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				if (writer.ShouldSerialiseProperty(property.Member, elementType))
					PrepareArrayObjectReferences(property.Reader(element), property.Member.PropertyType, objectHistoryIfReferenceReuseAllowed, newObjectReferencesFromArray, writer);
			}
		}

		internal static bool IsTreatedAsPrimitive(Type type) =>
			type.IsPrimitive || type.IsEnum || (type == CommonTypeOfs.DateTime) || (type == CommonTypeOfs.TimeSpan) || (type == CommonTypeOfs.String) || (type == CommonTypeOfs.Guid);

		// Courtesy of https://stackoverflow.com/a/41169463/3813189
		private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
		{
			public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}
}