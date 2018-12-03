using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DanSerialiser.BinaryTypeStructures;
using DanSerialiser.CachedLookups;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	public sealed class BinarySerialisationReader
	{
		private readonly Stream _stream;
		private readonly IDeserialisationTypeConverter[] _typeConverters;
		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		private readonly Dictionary<int, string> _nameReferences;
		private readonly Dictionary<int, object> _objectReferences;
		private readonly HashSet<int> _deferredInitialisationReferenceIDsAwaitingPopulation;
		private readonly Dictionary<int, BinarySerialisationReaderTypeReader> _typeReaders;
		private bool _haveEncounteredDeferredInitialisationObject;
		public BinarySerialisationReader(Stream stream) : this(stream, new IDeserialisationTypeConverter[0], DefaultTypeAnalyser.Instance) { }
		public BinarySerialisationReader(Stream stream, IDeserialisationTypeConverter[] typeConverters) : this(stream, typeConverters, DefaultTypeAnalyser.Instance) { }
		internal BinarySerialisationReader(Stream stream, IDeserialisationTypeConverter[] typeConverters, IAnalyseTypesForSerialisation typeAnalyser) // internal constructor may be used by unit tests
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_typeConverters = typeConverters ?? throw new ArgumentNullException(nameof(typeConverters));
			_typeAnalyser = typeAnalyser ?? throw new ArgumentNullException(nameof(typeAnalyser));

			_nameReferences = new Dictionary<int, string>();
			_objectReferences = new Dictionary<int, object>();
			_deferredInitialisationReferenceIDsAwaitingPopulation = new HashSet<int>();

			// We can take adantage of the fact that the fields for each distinct type will always appear in the same order (and there will always be the precise same number of
			// field entries for subsequent occurrences of a particular type) AND the field names will always use Name Reference IDs after the first time that a type is written
			// out. So, instead of treating the field data as dynamic content and having to try to resolve the fields each time that an object is to be deserialised, we can
			// build specialised deserialisers for each type that know what fields are going to appear and that have cached the field lookup data - this will mean that there is
			// a lot less work to do when there are many instances of a given type within a payload. Also note that after the first appearance of a type, Name Reference IDs will
			// always be used for the type name and so the lookup that is constructed to these per-type deserialisers has an int key (which is less work to match than using the
			// full type name as the key would be).
			// - This is confused if the BinarySerialisationWriter is optimised for wide circular references because it shifts object definitions around and so the type readers
			//   can't be used in that case (which means that performance per-instance deserialisation performance is reduced but it would stack overflow otherwise, so it's not
			//   really compromised since the optimised-for-tree approach might not work at all!)
			_typeReaders = new Dictionary<int, BinarySerialisationReaderTypeReader>();

			_haveEncounteredDeferredInitialisationObject = false;
		}

		public T Read<T>()
		{
			// The original intention of the use of generic type params for the reader and writer was to reduce casting at the call sites and I had thought that the type might
			// be required when deserialising - but it is not for the current BinaryWriter and BinaryReader implementations, so all we do with T here is try to cast the return
			// value to it
			// Note: As this is the top level "Read" call, if it is an object being deserialised and the type of that object is not available then the deserialisation attempt
			// should fail, which is why ignoreAnyInvalidTypes is passed as false (that option only applies in cases where a nested object is being deserialised but the field
			// that it would be used to set does not exist on the parent object - if there is no field to set, who cares if the value is of a type that is not available)
			var result = Read(ignoreAnyInvalidTypes: false, targetTypeIfAvailable: typeof(T));
			if (_deferredInitialisationReferenceIDsAwaitingPopulation.Count > 0)
				throw new InvalidSerialisationDataFormatException("There were deferred-initialisation references encountered in the data that did not get fully populated before the stream ended");
			return (T)result;
		}

		internal object Read(bool ignoreAnyInvalidTypes, Type targetTypeIfAvailable)
		{
			var dataType = ProcessAnyTypeAndFieldNamePreLoadsAndReturnNextDataType();
			var value = ReadBeforeApplyingAnyTransforms(dataType, ignoreAnyInvalidTypes);

			// Give the type converters a crack at the current value - if any of them change the value then take that as the new value and don't consider any other converters
			// (don't do this if targetTypeIfAvailable is unavailable because the type converters expect a non-null value for it - it's acceptable to get here and it be null,
			// though, as that is what will happen if the serialised data has information for a field that exist in the version of the entity currently being deserialised into)
			if (targetTypeIfAvailable != null)
			{
				foreach (var typeConverter in _typeConverters)
				{
					var updatedValue = typeConverter.ConvertIfRequired(targetTypeIfAvailable, value);
					if (!ReferenceEquals(updatedValue, targetTypeIfAvailable))
						return updatedValue;
				}
			}
			return value;
		}

		private object ReadBeforeApplyingAnyTransforms(BinarySerialisationDataType dataType, bool ignoreAnyInvalidTypes)
		{
			switch (dataType)
			{
				default:
					throw new InvalidSerialisationDataFormatException("Unexpected BinarySerialisationDataType: " + dataType);

				case BinarySerialisationDataType.Null:
					return null;

				case BinarySerialisationDataType.Boolean:
					return ReadNext() != 0;
				case BinarySerialisationDataType.Byte:
					return ReadNext();
				case BinarySerialisationDataType.SByte:
					return (sbyte)ReadNext();

				case BinarySerialisationDataType.Int16:
					return ReadNextInt16();
				case BinarySerialisationDataType.Int32_8:
					return (int)ReadNext();
				case BinarySerialisationDataType.Int32_16:
					return (int)ReadNextInt16();
				case BinarySerialisationDataType.Int32_24:
					return ReadNextInt24();
				case BinarySerialisationDataType.Int32:
					return ReadNextInt();
				case BinarySerialisationDataType.Int64:
					return ReadNextInt64();

				case BinarySerialisationDataType.UInt16:
					return (new UInt16Bytes(ReadNext(Int16Bytes.BytesRequired))).Value;
				case BinarySerialisationDataType.UInt32:
					return (new UInt32Bytes(ReadNext(Int32Bytes.BytesRequired))).Value;
				case BinarySerialisationDataType.UInt64:
					return (new UInt64Bytes(ReadNext(Int64Bytes.BytesRequired))).Value;

				case BinarySerialisationDataType.Single:
					return (new SingleBytes(ReadNext(SingleBytes.BytesRequired))).Value;
				case BinarySerialisationDataType.Double:
					return (new DoubleBytes(ReadNext(DoubleBytes.BytesRequired))).Value;
				case BinarySerialisationDataType.Decimal:
					return (new DecimalBytes(ReadNext(DecimalBytes.BytesRequired))).Value;

				case BinarySerialisationDataType.Char:
					return (new CharBytes(ReadNext(CharBytes.BytesRequired))).Value;
				case BinarySerialisationDataType.String:
					return ReadNextString();

				case BinarySerialisationDataType.DateTime:
					return ReadNextDateTime();
				case BinarySerialisationDataType.TimeSpan:
					return ReadNextTimeSpan();

				case BinarySerialisationDataType.Guid:
					return ReadNextGuid();

				case BinarySerialisationDataType.ArrayStart:
					return ReadNextArray(ignoreAnyInvalidTypes);

				case BinarySerialisationDataType.ObjectStart:
					return ReadNextObject(ignoreAnyInvalidTypes, toPopulateDeferredInstance: false);
			}
		}

		private short ReadNextInt16()
		{
			return (new Int16Bytes(ReadNext(Int16Bytes.BytesRequired))).Value;
		}

		private int ReadNextInt24()
		{
			return (new Int24Bytes(ReadNext(Int24Bytes.BytesRequired))).Value;
		}

		private int ReadNextInt()
		{
			return (new Int32Bytes(ReadNext(Int32Bytes.BytesRequired))).Value;
		}

		private long ReadNextInt64()
		{
			return (new Int64Bytes(ReadNext(Int64Bytes.BytesRequired))).Value;
		}

		private string ReadNextString()
		{
			var dataType = ReadNextDataType();
			int length;
			if (dataType == BinarySerialisationDataType.Int32_8)
				length = ReadNext();
			else if (dataType == BinarySerialisationDataType.Int32_16)
				length = ReadNextInt16();
			else if (dataType == BinarySerialisationDataType.Int32_24)
				length = ReadNextInt24();
			else if (dataType == BinarySerialisationDataType.Int32)
				length = ReadNextInt();
			else
				throw new InvalidSerialisationDataFormatException("Unexpected BinarySerialisationDataType for String length: " + dataType);

			if (length == -1)
				return null;
			if (length == 0)
				return "";
			return Encoding.UTF8.GetString(ReadNext(length));
		}

		private DateTime ReadNextDateTime() => new DateTime(ReadNextInt64(), (DateTimeKind)ReadNext());

		private TimeSpan ReadNextTimeSpan() => TimeSpan.FromTicks(ReadNextInt64());

		private Guid ReadNextGuid() => new Guid(ReadNext(16));

		private object ReadNextObject(bool ignoreAnyInvalidTypes, bool toPopulateDeferredInstance)
		{
			var typeName = ReadNextTypeName(out var typeNameReferenceID);
			if (typeName == null)
				throw new InvalidSerialisationDataFormatException("Null type names should not exist in object data since there is a Null binary serialisation data type");

			// If the next value is a Reference ID then the writer had supportReferenceReuse and all object definitions for reference types (except strings) will start with a
			// Reference ID that will either be a new ID (followed by the object data) or an existing ID (followed by ObjectEnd)
			int? referenceID;
			var nextEntryType = ReadNextDataType();
			if (nextEntryType == BinarySerialisationDataType.ReferenceID8)
				referenceID = ReadNext();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID16)
				referenceID = ReadNextInt16();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID24)
				referenceID = ReadNextInt24();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID32)
				referenceID = ReadNext();
			else
				referenceID = null;
			object alreadyEncounteredReference;
			if (referenceID != null)
			{
				if (referenceID < 0)
					throw new InvalidSerialisationDataFormatException("Encountered negative Reference ID, invalid:" + referenceID);
				if (_objectReferences.TryGetValue(referenceID.Value, out alreadyEncounteredReference))
				{
					// This is an existing Reference ID so ensure that it's followed by ObjectEnd and return the existing reference.. unless it is an object reference that was
					// created as a placeholder and now needs to be fully populated (whicch is only the case if BinarySerialisationWriter is optimised for wide circular references,
					// as opposed to tree structures)
					if (!toPopulateDeferredInstance)
					{
						nextEntryType = ReadNextDataType();
						if (nextEntryType != BinarySerialisationDataType.ObjectEnd)
							throw new InvalidSerialisationDataFormatException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} was not encountered after reused reference");
						return alreadyEncounteredReference;
					}
					else
					{
						_haveEncounteredDeferredInitialisationObject = true;
					}
				}
				nextEntryType = ReadNextDataType();
			} 
			else
			{
				referenceID = null;
				alreadyEncounteredReference = null;
			}

			// If we have already encountered this type then we should have a BinarySerialisationReaderTypeReader instance prepared that loops through the field data quickly,
			// rather than having to perform the more expensive work below (with its type name resolution and field name lookups)
			// - Note: If the content that is being deserialised include deferred-population object references (which is the case if the BinarySerialisationWriter was set up
			//   to be optimised for wide circular references) then we can't use these optimised type builders because sometimes we need to create new populated instances and
			//   somtimes we have to populate initialised-but-unpopulated references)
			if (!_haveEncounteredDeferredInitialisationObject && _typeReaders.TryGetValue(typeNameReferenceID, out var typeReader))
			{
				var instance = typeReader.GetUninitialisedInstance();
				if (referenceID != null)
				{
					// If there is an Object Reference ID for the current object then we need to push the uninitialised instance into the objectReferences dictionary before
					// trying to populate because there is a chance that properties on it will include a circular references back to this object and if there is no
					// objectReferences entry then that will cause much confusion (see BinarySerialisationTests_SupportReferenceReUseInMostlyTreeLikeStructure's
					// "CircularReferencesAreSupportedWhereTheSameTypeIsEncounteredMultipleTimes" test method for an example)
					_objectReferences[referenceID.Value] = instance;
				}
				return typeReader.ReadInto(instance, this, nextEntryType, ignoreAnyInvalidTypes);
			}

			// Try to get a type builder for the type that we should be deserialising to. If ignoreAnyInvalidTypes is true then don't worry if we can't find the type that is
			// specified because we don't care about the return value from this method, we're just parsing the data to progress to the next data that we DO care about.
			var typeBuilderIfAvailable = _typeAnalyser.TryToGetUninitialisedInstanceBuilder(typeName);
			if ((typeBuilderIfAvailable == null) && !ignoreAnyInvalidTypes)
				throw new TypeLoadException("Unable to load type " + typeName);
			object valueIfTypeIsAvailable;
			if (toPopulateDeferredInstance)
			{
				// If this is a deferred-initialisation reference then we already have a reference but it hasn't been populated yet - so use that reference instead of
				// creating a new one
				valueIfTypeIsAvailable = alreadyEncounteredReference;
			}
			else
				valueIfTypeIsAvailable = (typeBuilderIfAvailable == null) ? null : typeBuilderIfAvailable();
			if ((valueIfTypeIsAvailable != null) && (referenceID != null))
			{
				// If this is data from a BinarySerialisationWriter that was optimised for wide circular references then some object references will be passed through twice;
				// once to create an non-populated placeholder and again to set all of the fields (but if the writer was optimised for trees then each object reference will
				// only ever be set once). Since there is a chance that we will encounter a reference twice, we COULD check ContainsKey and THEN call Add OR we could just
				// set the item once and do the lookup / find-insert-point only once (and since it will be the same reference that is going in to the particular slot in
				// the objectReferences dictionary - though potentially twice - then there is no risk of replacing a reference by accident)
				_objectReferences[referenceID.Value] = valueIfTypeIsAvailable;
			}

			// If the BinarySerialisationWriter was optimised for wide circular references then the first instance of an object definition may include an "ObjectContentPostponed"
			// flag to indicate that the instance should be created now but it will be unpopulated (for now.. it will be populated in a second pass later in). We'll track the
			// Reference IDs of deferred-initialisation / postponed-content references because we want to make sure that they all get fully initialised before the deserialisation
			// process completes
			if (nextEntryType == BinarySerialisationDataType.ObjectContentPostponed)
			{
				if (referenceID == null)
					throw new InvalidSerialisationDataFormatException(nameof(BinarySerialisationDataType.ObjectContentPostponed) + " should always appear with a ReferenceID");
				nextEntryType = ReadNextDataType();
				if (nextEntryType != BinarySerialisationDataType.ObjectEnd)
					throw new InvalidSerialisationDataFormatException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} after {nameof(BinarySerialisationDataType.ObjectContentPostponed)} but encountered {nextEntryType}");
				_deferredInitialisationReferenceIDsAwaitingPopulation.Add(referenceID.Value);
				return valueIfTypeIsAvailable;
			}
			else if (toPopulateDeferredInstance)
			{
				if (referenceID == null)
					throw new InvalidSerialisationDataFormatException($"There should always be a ReferenceID when {nameof(toPopulateDeferredInstance)} is true");
				_deferredInitialisationReferenceIDsAwaitingPopulation.Remove(referenceID.Value);
			}

			var typeIfAvailable = valueIfTypeIsAvailable?.GetType();
			var fieldsThatHaveBeenSet = new List<FieldInfo>();
			var fieldSettingInformationForGeneratingTypeBuilder = new List<BinarySerialisationReaderTypeReader.FieldSettingDetails>();
			while (true)
			{
				if (nextEntryType == BinarySerialisationDataType.ObjectEnd)
				{
					if (valueIfTypeIsAvailable != null)
					{
						var fieldsThatShouldHaveBeenSet = _typeAnalyser.GetAllFieldsThatShouldBeSet(typeIfAvailable);
						if (fieldsThatHaveBeenSet.Count != fieldsThatShouldHaveBeenSet.Length)
						{
							foreach (var mandatoryField in fieldsThatShouldHaveBeenSet)
							{
								if (fieldsThatHaveBeenSet.Find(fieldThatHasBeenSet => (fieldThatHasBeenSet.DeclaringType == mandatoryField.DeclaringType) && (fieldThatHasBeenSet.Name == mandatoryField.Name)) == null)
									throw new FieldNotPresentInSerialisedDataException(mandatoryField.DeclaringType.AssemblyQualifiedName, mandatoryField.Name);
							}
						}
					}
					if (typeBuilderIfAvailable != null)
					{
						// Create a BinarySerialisationReaderTypeReader instance for this type so that we can reuse the field name lookup results that we had to do in this
						// loop next time (this works because the BinarySerialisationWriter will always write out the same field data in the same order for a given type,
						// so if the data was valid this time - no missing required fields, for example - then it will be valid next time and we can read the data much
						// mroe quickly)
						_typeReaders[typeNameReferenceID] = new BinarySerialisationReaderTypeReader(typeBuilderIfAvailable, fieldSettingInformationForGeneratingTypeBuilder.ToArray());
					}
					return valueIfTypeIsAvailable;
				}
				else if (nextEntryType == BinarySerialisationDataType.FieldName)
				{
					nextEntryType = ReadNextDataType();
					string rawFieldNameInformation;
					int fieldNameReferenceID;
					if (nextEntryType == BinarySerialisationDataType.String)
					{
						rawFieldNameInformation = ReadNextString();
						fieldNameReferenceID = ReadNextNameReferenceID(ReadNextDataType());
						_nameReferences[fieldNameReferenceID] = rawFieldNameInformation;
					}
					else
					{
						fieldNameReferenceID = ReadNextNameReferenceID(nextEntryType);
						if (!_nameReferences.TryGetValue(fieldNameReferenceID, out rawFieldNameInformation))
							throw new InvalidSerialisationDataFormatException("Invalid NameReferenceID: " + fieldNameReferenceID);
					}
					BinaryReaderWriterShared.SplitCombinedTypeAndFieldName(rawFieldNameInformation, out var typeNameIfRequired, out var fieldName);

					// Try to get a reference to the field on the target type.. if there is one (if valueIfTypeIsAvailable is null then no-one cases about this data and we're just
					// parsing it to skip over it)
					var field = (valueIfTypeIsAvailable == null) ? null : _typeAnalyser.TryToFindField(typeIfAvailable, fieldName, typeNameIfRequired);

					// Note: If the field doesn't exist then parse the data but don't worry about any types not being available because we're not going to set anything to the value
					// that we get back from the "Read" call (but we still need to parse that data to advance the reader to the next field or the end of the current object)
					var fieldValue = Read(ignoreAnyInvalidTypes: (field == null), targetTypeIfAvailable: field?.Member?.FieldType);

					// Now that we have the value to set the field to IF IT EXISTS, try to set the field.. if it's a field that we've already identified on the type then it's easy.
					// However, it may also have been a field on an older version of the type when it was serialised and now that it's deserialised, we'll need to check for any
					// properties marked with [Deprecated] that we can set with the value that then set the fields that replaced the deprecated field (if this is the case then
					// field will currently be null but valueIfTypeIsAvailable will not be null).
					if (field != null)
					{
						if (field.WriterUnlessFieldShouldBeIgnored != null)
						{
							// The WriterUnlessFieldShouldBeIgnored has to take a "ref" argument in order to update structs but this will cause a problem if valueIfTypeIsAvailable
							// is an object reference that isn't just the base object type (we'll need to create a reference that IS type "object" to pass in - but it will be the
							// same reference and so it doesn't matter that we're setting the field on it indirectly)
							if ((typeIfAvailable == CommonTypeOfs.Object) || typeIfAvailable.IsValueType)
								field.WriterUnlessFieldShouldBeIgnored(ref valueIfTypeIsAvailable, fieldValue);
							else
							{
								object refForUpdate = valueIfTypeIsAvailable;
								field.WriterUnlessFieldShouldBeIgnored(ref refForUpdate, fieldValue);
							}
							fieldsThatHaveBeenSet.Add(field.Member);
						}
						fieldSettingInformationForGeneratingTypeBuilder.Add(new BinarySerialisationReaderTypeReader.FieldSettingDetails(
							fieldNameReferenceID,
							field.Member.FieldType,
							(field.WriterUnlessFieldShouldBeIgnored == null) ? new MemberUpdater[0] : new[] { field.WriterUnlessFieldShouldBeIgnored }
						));
					}
					else if (valueIfTypeIsAvailable != null)
					{
						// We successfully deserialised a value but couldn't directly set a field to it - before giving up, there's something else to check.. if an older version
						// of a type was serialised that did have the field that we've got a value for and the target type is a newer version of that type that has a [Deprecated]
						// property that can map the old field onto a new field / property then we should try to set the [Deprecated] property's value to the value that we have.
						// That [Deprecated] property's setter should then set a property / field on the new version of the type. If that is the case, then we can add that new
						// property / field to the have-successfully-set list.
						var deprecatedPropertySettingDetails = _typeAnalyser.TryToGetPropertySettersAndFieldsToConsiderToHaveBeenSet(typeIfAvailable, fieldName, typeNameIfRequired, fieldValue?.GetType());
						if (deprecatedPropertySettingDetails != null)
						{
							foreach (var propertySetter in deprecatedPropertySettingDetails.PropertySetters)
								propertySetter(ref valueIfTypeIsAvailable, fieldValue);
							fieldsThatHaveBeenSet.AddRange(deprecatedPropertySettingDetails.RelatedFieldsThatHaveBeenSetViaTheDeprecatedProperties);
							fieldSettingInformationForGeneratingTypeBuilder.Add(new BinarySerialisationReaderTypeReader.FieldSettingDetails(
								fieldNameReferenceID,
								deprecatedPropertySettingDetails.CompatibleTypeToReadAs,
								deprecatedPropertySettingDetails.PropertySetters
							));
						}
					}
				}
				else
					throw new InvalidSerialisationDataFormatException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
				nextEntryType = ReadNextDataType();
			}
		}

		private void PrepareObjectDeferred()
		{
			var typeName = ReadNextTypeName(out var typeNameReferenceID);
			if (typeName == null)
				throw new InvalidSerialisationDataFormatException("Null type names should not exist in object data since there is a Null binary serialisation data type");

			int referenceID;
			var nextEntryType = ReadNextDataType();
			if (nextEntryType == BinarySerialisationDataType.ReferenceID8)
				referenceID = ReadNext();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID16)
				referenceID = ReadNextInt16();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID24)
				referenceID = ReadNextInt24();
			else if (nextEntryType == BinarySerialisationDataType.ReferenceID32)
				referenceID = ReadNext();
			else
				throw new InvalidSerialisationDataFormatException("Expected Object Reference ID for " + nameof(BinarySerialisationDataType.ObjectContentPostponed));
			if (referenceID < 0)
				throw new InvalidSerialisationDataFormatException("Encountered negative Reference ID, invalid:" + referenceID);
			if (_objectReferences.ContainsKey(referenceID))
				throw new InvalidSerialisationDataFormatException($"Don't expect {nameof(BinarySerialisationDataType.ObjectContentPostponed)} for object reference that has already been populated");

		}

		internal int ReadNextNameReferenceID(BinarySerialisationDataType specifiedDataType)
		{
			if (specifiedDataType == BinarySerialisationDataType.NameReferenceID32)
				return ReadNextInt();
			else if (specifiedDataType == BinarySerialisationDataType.NameReferenceID24)
				return ReadNextInt24();
			else if (specifiedDataType == BinarySerialisationDataType.NameReferenceID16)
				return ReadNextInt16();
			else if (specifiedDataType == BinarySerialisationDataType.NameReferenceID8)
				return ReadNext();
			else
				throw new InvalidSerialisationDataFormatException("Unexpected " + specifiedDataType + " (expected a Name Reference ID)");
		}

		private object ReadNextArray(bool ignoreAnyInvalidTypes)
		{
			var elementTypeName = ReadNextTypeName(out var typeNameReferenceID);
			if (elementTypeName == null)
				throw new InvalidSerialisationDataFormatException("Null array element type names should not exist in object data since there is a Null binary serialisation data type");

			var elementType = _typeAnalyser.GetType(elementTypeName); // These lookups will be cached by the_typeAnalyser, which can help (vs calling Type.GetType every time)
			var lengthDataType = ReadNextDataType();
			int length;
			if (lengthDataType == BinarySerialisationDataType.Int32_8)
				length = ReadNext();
			else if (lengthDataType == BinarySerialisationDataType.Int32_16)
				length = ReadNextInt16();
			else if (lengthDataType == BinarySerialisationDataType.Int32_24)
				length = ReadNextInt24();
			else if (lengthDataType == BinarySerialisationDataType.Int32)
				length = ReadNextInt();
			else
				throw new InvalidSerialisationDataFormatException("Unexpected BinarySerialisationDataType for Array length: " + lengthDataType);

			// When an array contains enum elements, the underlying value is written by the serialisation process and so we'll need to read that value back out and then cast it to
			// the enum type, otherwise the array SetValue call will fail. In order to do that, we need to check now whether the element type is an enum OR if the element type is
			// a Nullable enum because the same rules apply to that (a Nullable Nullable enum is not supported, so there is no recursive checks required).
			Type enumCastTargetTypeIfRequired;
			if (length == 0)
				enumCastTargetTypeIfRequired = null;
			else
			{
				if (elementType.IsEnum)
					enumCastTargetTypeIfRequired = elementType;
				else
				{
					var nullableInnerTypeIfApplicable = Nullable.GetUnderlyingType(elementType);
					enumCastTargetTypeIfRequired = ((nullableInnerTypeIfApplicable != null) && nullableInnerTypeIfApplicable.IsEnum) ? nullableInnerTypeIfApplicable : null;
				}
			}

			var items = Array.CreateInstance(elementType, length);
			for (var index = 0; index < items.Length; index++)
			{
				var element = Read(ignoreAnyInvalidTypes, elementType);
				if ((enumCastTargetTypeIfRequired != null) && (element != null))
				{
					if (element is byte b)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, b);
					else if (element is short s)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, s);
					else if (element is int i)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, i);
					else if (element is long l)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, l);
					else if (element is sbyte sb)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, sb);
					else if (element is ushort us)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, us);
					else if (element is uint ui)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, ui);
					else if (element is ulong ul)
						element = Enum.ToObject(enumCastTargetTypeIfRequired, ul);
					else
						element = Enum.ToObject(enumCastTargetTypeIfRequired, element);
				}
				items.SetValue(element, index);
			}

			// If the BinarySerialisationWriter is configured to optimise for wide circular references then there may be some content-for-delay-populated-objects here
			// after the array (but if it was configured for trees then there won't be any deferred object populations and we'll go straight to ArrayEnd)
			var nextEntryType = ReadNextDataType();
			while (nextEntryType != BinarySerialisationDataType.ArrayEnd)
			{
				if (nextEntryType != BinarySerialisationDataType.ObjectStart)
					throw new InvalidSerialisationDataFormatException($"After array elements, expected either {nameof(BinarySerialisationDataType.ArrayEnd)} or {nameof(BinarySerialisationDataType.ObjectStart)} (for deferred references)");
				ReadNextObject(ignoreAnyInvalidTypes, toPopulateDeferredInstance: true);
				nextEntryType = ReadNextDataType();
			}
			if (nextEntryType != BinarySerialisationDataType.ArrayEnd)
				throw new InvalidSerialisationDataFormatException($"Expected {nameof(BinarySerialisationDataType.ArrayEnd)} was not encountered");
			return items;
		}

		private string ReadNextTypeName(out int typeNameReferenceID)
		{
			// If a null type name is written out then there will be no Name Reference ID but every non-null type name will either be represented by a string and then the
			// Name Reference ID that that string should be recorded as or it just be a Name Reference ID for a reference that has already been encountered
			var nextEntryType = ReadNextDataType();
			if (nextEntryType == BinarySerialisationDataType.String)
			{
				var typeName = ReadNextString();
				typeNameReferenceID = ReadNextNameReferenceID(ReadNextDataType());
				if (typeName != null)
					_nameReferences[typeNameReferenceID] = typeName;
				return typeName;
			}
			else
			{
				typeNameReferenceID = ReadNextNameReferenceID(nextEntryType);
				if (!_nameReferences.TryGetValue(typeNameReferenceID, out var typeName))
					throw new InvalidSerialisationDataFormatException("Invalid NameReferenceID: " + typeNameReferenceID);
				return typeName;
			}
		}

		private BinarySerialisationDataType ProcessAnyTypeAndFieldNamePreLoadsAndReturnNextDataType()
		{
			while (true)
			{
				var dataType = ReadNextDataType();
				if ((dataType == BinarySerialisationDataType.TypeNamePreLoad) || (dataType == BinarySerialisationDataType.FieldNamePreLoad))
				{
					if (ReadNextDataType() != BinarySerialisationDataType.String)
						throw new InvalidSerialisationDataFormatException("Expected String value after " + dataType);
					var typeOrFieldName = ReadNextString();
					var nameReferenceID = ReadNextNameReferenceID(ReadNextDataType());
					_nameReferences[nameReferenceID] = typeOrFieldName;
				}
				else
					return dataType;
			}
		}

		internal BinarySerialisationDataType ReadNextDataType()
		{
			return (BinarySerialisationDataType)ReadNext();
		}

		private byte ReadNext()
		{
			var value = _stream.ReadByte(); // Returns -1 if no data or the byte cast to an int if there IS data
			if (value == -1)
				throw new InvalidSerialisationDataFormatException("Insufficient data to read (presume invalid content)");
			return (byte)value;
		}

		private byte[] ReadNext(int numberOfBytes)
		{
			var values = new byte[numberOfBytes];
			if (_stream.Read(values, 0, numberOfBytes) < numberOfBytes) // Returns number of bytes read (less than numberOfBytes if insufficient data)
				throw new InvalidSerialisationDataFormatException("Insufficient data to read (presume invalid content)");
			return values;
		}
	}
}