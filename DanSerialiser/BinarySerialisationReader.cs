using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	public sealed class BinarySerialisationReader
	{
		private readonly Stream _stream;
		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		private readonly Dictionary<int, string> _nameReferences;
		private readonly List<object> _objectReferences;
		public BinarySerialisationReader(Stream stream) : this(stream, DefaultTypeAnalyser.Instance) { }
		internal BinarySerialisationReader(Stream stream, IAnalyseTypesForSerialisation typeAnalyser) // internal constructor may be used by unit tests
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_typeAnalyser = typeAnalyser ?? throw new ArgumentNullException(nameof(typeAnalyser));

			// Object Reference IDs that appear in the data will appear in ascending numerical order (with no gaps), which means that it is safe to store them in a list so that
			// read access is performed via the index - this is possible to achieve when serialising because the Object Reference IDs are scoped to the particular writer instance
			// whereas the Name Reference IDs are managed by the BinarySerialisationWriterCachedNames and so the Name Reference IDs encountered  to serialise a particular object
			// may not be continuous and so a dictionary is required to describe a lookup for them here.
			_nameReferences = new Dictionary<int, string>();
			_objectReferences = new List<object>();
		}

		public T Read<T>()
		{
			// The original intention of the use of generic type params for the reader and writer was to reduce casting at the call sites and I had thought that the type might
			// be required when deserialising - but it is not for the current BinaryWriter and BinaryReader implementations, so all we do with T here is try to cast the return
			// value to it
			// Note: As this is the top level "Read" call, if it is an object being deserialised and the type of that object is not available then the deserialisation attempt
			// should fail, which is why ignoreAnyInvalidTypes is passed as false (that option only applies in cases where a nested object is being deserialised but the field
			// that it would be used to set does not exist on the parent object - if there is no field to set, who cares if the value is of a type that is not available)
			return (T)Read(ignoreAnyInvalidTypes: false);
		}

		private object Read(bool ignoreAnyInvalidTypes)
		{
			switch (ReadNextDataType())
			{
				default:
					throw new NotImplementedException();

				case BinarySerialisationDataType.Boolean:
					return ReadNext() != 0;
				case BinarySerialisationDataType.Byte:
					return ReadNext();
				case BinarySerialisationDataType.SByte:
					return (sbyte)ReadNext();

				case BinarySerialisationDataType.Int16:
					return ReadNextInt16();
				case BinarySerialisationDataType.Int32:
					return ReadNextInt();
				case BinarySerialisationDataType.Int32_8:
					return (int)ReadNext();
				case BinarySerialisationDataType.Int32_16:
					return (int)ReadNextInt16();
				case BinarySerialisationDataType.Int32_24:
					return ReadNextInt24();
				case BinarySerialisationDataType.Int64:
					return ReadNextInt64();

				case BinarySerialisationDataType.UInt16:
					return BitConverter.ToUInt16(ReadNext(sizeof(UInt16)), 0);
				case BinarySerialisationDataType.UInt32:
					return BitConverter.ToUInt32(ReadNext(sizeof(UInt32)), 0);
				case BinarySerialisationDataType.UInt64:
					return BitConverter.ToUInt64(ReadNext(sizeof(UInt64)), 0);

				case BinarySerialisationDataType.Single:
					return BitConverter.ToSingle(ReadNext(sizeof(Single)), 0);
				case BinarySerialisationDataType.Double:
					return BitConverter.ToDouble(ReadNext(sizeof(Double)), 0);
				case BinarySerialisationDataType.Decimal:
					// BitConverter does not deal with decimal (there is no GetBytes overloads for it and no ToDecimal method) so BinaryWriter used decimal.GetBits, which
					// returns four int values and so we need to do the opposite here
					var partialValues = new int[4];
					for (var i = 0; i < 4; i++)
						partialValues[i] = ReadNextInt();
					return new decimal(partialValues);

				case BinarySerialisationDataType.Char:
					return BitConverter.ToChar(ReadNext(sizeof(Char)), 0);
				case BinarySerialisationDataType.String:
					return ReadNextString();

				case BinarySerialisationDataType.DateTime:
					return ReadNextDateTime();

				case BinarySerialisationDataType.ArrayStart:
					return ReadNextArray(ignoreAnyInvalidTypes);

				case BinarySerialisationDataType.ObjectStart:
					return ReadNextObject(ignoreAnyInvalidTypes);
			}
		}

		private short ReadNextInt16()
		{
			return (short)((ReadNext() << 8) + ReadNext());
		}

		private int ReadNextInt24()
		{
			return (ReadNext() << 16) + (ReadNext() << 8) + ReadNext();
		}

		private int ReadNextInt()
		{
			return (ReadNext() << 24) + (ReadNext() << 16) + (ReadNext() << 8) + ReadNext();
		}

		private long ReadNextInt64()
		{
			return ((long)ReadNext() << 56) + ((long)ReadNext() << 48) + ((long)ReadNext() << 40) + ((long)ReadNext() << 32) + ((long)ReadNext() << 24) + ((long)ReadNext() << 16) + ((long)ReadNext() << 8) + ReadNext();
		}

		private string ReadNextString()
		{
			var length = ReadNextInt();
			return (length == -1) ? null : Encoding.UTF8.GetString(ReadNext(length));
		}

		private DateTime ReadNextDateTime()
		{
			return new DateTime(ReadNextInt64(), (DateTimeKind)ReadNext());
		}

		private object ReadNextObject(bool ignoreAnyInvalidTypes)
		{
			var typeName = ReadNextTypeName();
			if (typeName == null)
			{
				if (ReadNextDataType() != BinarySerialisationDataType.ObjectEnd)
					throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} was not encountered after null value");
				return null;
			}

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
			if (referenceID != null)
			{
				if (referenceID < 0)
					throw new Exception("Encountered negative Reference ID, invalid:" + referenceID);
				if (referenceID.Value < _objectReferences.Count)
				{
					// This is an existing Reference ID so ensure that it's followed by ObjectEnd and return the existing reference
					if (ReadNextDataType() != BinarySerialisationDataType.ObjectEnd)
						throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} was not encountered after reused reference");
					return _objectReferences[referenceID.Value];
				}
				nextEntryType = ReadNextDataType();
			}
			else
				referenceID = null;

			// Try to get a type builder for the type that we should be deserialising to. If ignoreAnyInvalidTypes is true then don't worry if we can't find the type that is
			// specified because we don't care about the return value from this method, we're just parsing the data to progress to the next data that we DO care about.
			var typeBuilderIfAvailable = _typeAnalyser.TryToGetUninitialisedInstanceBuilder(typeName);
			if ((typeBuilderIfAvailable == null) && !ignoreAnyInvalidTypes)
				throw new TypeLoadException("Unable to load type " + typeName);
			var valueIfTypeIsAvailable = (typeBuilderIfAvailable == null) ? null : typeBuilderIfAvailable();
			if ((valueIfTypeIsAvailable != null) && (referenceID != null))
				_objectReferences.Add(valueIfTypeIsAvailable);
			var typeIfAvailable = valueIfTypeIsAvailable?.GetType();
			var fieldsThatHaveBeenSet = new List<FieldInfo>();
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
					return valueIfTypeIsAvailable;
				}
				else if (nextEntryType == BinarySerialisationDataType.FieldName)
				{
					nextEntryType = ReadNextDataType();
					string rawFieldNameInformation;
					if (nextEntryType == BinarySerialisationDataType.String)
					{
						rawFieldNameInformation = ReadNextString();
						_nameReferences[ReadNextNameReferenceID(ReadNextDataType())] = rawFieldNameInformation;
					}
					else
					{
						var nameReferenceID = ReadNextNameReferenceID(nextEntryType);
						if (!_nameReferences.TryGetValue(nameReferenceID, out rawFieldNameInformation))
							throw new Exception("Invalid NameReferenceID: " + nameReferenceID);
					}
					BinaryReaderWriterShared.SplitCombinedTypeAndFieldName(rawFieldNameInformation, out var typeNameIfRequired, out var fieldName);

					// Try to get a reference to the field on the target type.. if there is one (if valueIfTypeIsAvailable is null then no-one cases about this data and we're just
					// parsing it to skip over it)
					var field = (valueIfTypeIsAvailable == null) ? null : _typeAnalyser.TryToFindField(typeIfAvailable, fieldName, typeNameIfRequired);

					// Note: If the field doesn't exist then parse the data but don't worry about any types not being available because we're not going to set anything to the value
					// that we get back from the "Read" call (but we still need to parse that data to advance the reader to the next field or the end of the current object)
					var fieldValue = Read(ignoreAnyInvalidTypes: (field == null));

					// Now that we have the value to set the field to IF IT EXISTS, try to set the field.. if it's a field that we've already identified on the type then it's easy.
					// However, it may also have been a field on an older version of the type when it was serialised and now that it's deserialised, we'll need to check for any
					// properties marked with [Deprecated] that we can set with the value that then set the fields that replaced the deprecated field (if this is the case then
					// field will currently be null but valueIfTypeIsAvailable will not be null).
					if (field != null)
					{
						if (field.WriterUnlessFieldShouldBeIgnored != null)
						{
							field.WriterUnlessFieldShouldBeIgnored(valueIfTypeIsAvailable, fieldValue);
							fieldsThatHaveBeenSet.Add(field.Member);
						}
					}
					else if (valueIfTypeIsAvailable != null)
					{
						// We successfully deserialised a value but couldn't directly set a field to it - before giving up, there's something else to check.. if an older version
						// of a type was serialised that did have the field that we've got a value for and the target type is a newer version of that type that has a [Deprecated]
						// property that can map the old field onto a new field / property then we should try to set the [Deprecated] property's value to the value that we have.
						// That [Deprecated] property's setter should then set a property / field on the new version of the type. If that is the case, then we can add that new
						// property / field to the have-successfully-set list.
						var (propertySetters, relatedFieldsThatHaveBeenSet) = _typeAnalyser.GetPropertySettersAndFieldsToConsiderToHaveBeenSet(typeIfAvailable, fieldName, typeNameIfRequired, fieldValue?.GetType());
						foreach (var propertySetter in propertySetters)
							propertySetter(valueIfTypeIsAvailable, fieldValue);
						fieldsThatHaveBeenSet.AddRange(relatedFieldsThatHaveBeenSet);
					}
				}
				else
					throw new InvalidOperationException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
				nextEntryType = ReadNextDataType();
			}
		}

		private int ReadNextNameReferenceID(BinarySerialisationDataType specifiedDataType)
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
				throw new Exception("Unexpected " + specifiedDataType + " (expected a Name Reference ID)");
		}

		private object ReadNextArray(bool ignoreAnyInvalidTypes)
		{
			var elementTypeName = ReadNextTypeName();
			if (elementTypeName == null)
			{
				// If the element type was recorded as null then it means that the array itself was null (and so the next character should be an ArrayEnd)
				if (ReadNextDataType() != BinarySerialisationDataType.ArrayEnd)
					throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ArrayEnd)} was not encountered");
				return null;
			}
			var elementType = Type.GetType(elementTypeName, throwOnError: true);
			var items = Array.CreateInstance(elementType, length: ReadNextInt());
			for (var i = 0; i < items.Length; i++)
				items.SetValue(Read(ignoreAnyInvalidTypes), i);
			var nextEntryType = ReadNextDataType();
			if (nextEntryType != BinarySerialisationDataType.ArrayEnd)
				throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ArrayEnd)} was not encountered");
			return items;
		}

		private string ReadNextTypeName()
		{
			// If a null type name is written out then there will be no Name Reference ID but every non-null type name will either be represented by a string and then the
			// Name Reference ID that that string should be recorded as or it just be a Name Reference ID for a reference that has already been encountered
			var nextEntryType = ReadNextDataType();
			if (nextEntryType == BinarySerialisationDataType.String)
			{
				var typeName = ReadNextString();
				if (typeName != null)
					_nameReferences[ReadNextNameReferenceID(ReadNextDataType())] = typeName;
				return typeName;
			}
			else
			{
				var nameReferenceID = ReadNextNameReferenceID(nextEntryType);
				if (!_nameReferences.TryGetValue(nameReferenceID, out var typeName))
					throw new Exception("Invalid NameReferenceID: " + nameReferenceID);
				return typeName;
			}
		}

		private BinarySerialisationDataType ReadNextDataType()
		{
			return (BinarySerialisationDataType)ReadNext();
		}

		private byte ReadNext()
		{
			var value = _stream.ReadByte(); // Returns -1 if no data or the byte cast to an int if there IS data
			if (value == -1)
				throw new InvalidOperationException("Insufficient data to read (presume invalid content)");
			return (byte)value;
		}

		private byte[] ReadNext(int numberOfBytes)
		{
			var values = new byte[numberOfBytes];
			if (_stream.Read(values, 0, numberOfBytes) < numberOfBytes) // Returns number of bytes read (less than numberOfBytes if insufficient data)
				throw new InvalidOperationException("Insufficient data to read (presume invalid content)");
			return values;
		}
	}
}