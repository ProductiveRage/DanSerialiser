using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	public sealed class BinarySerialisationReader
	{
		private readonly Stream _stream;
		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		private readonly Dictionary<int, string> _nameReferences;
		private readonly Dictionary<int, object> _objectReferences;
		public BinarySerialisationReader(Stream stream) : this(stream, DefaultTypeAnalyser.Instance) { }
		internal BinarySerialisationReader(Stream stream, IAnalyseTypesForSerialisation typeAnalyser) // internal constructor may be used by unit tests
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_typeAnalyser = typeAnalyser ?? throw new ArgumentNullException(nameof(typeAnalyser));
			_nameReferences = new Dictionary<int, string>();
			_objectReferences = new Dictionary<int, object>();
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
					return BitConverter.ToInt16(ReadNext(sizeof(Int16)), 0);
				case BinarySerialisationDataType.Int32:
					return ReadNextInt();
				case BinarySerialisationDataType.Int64:
					return BitConverter.ToInt64(ReadNext(sizeof(Int64)), 0);

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

				case BinarySerialisationDataType.ArrayStart:
					return ReadNextArray(ignoreAnyInvalidTypes);

				case BinarySerialisationDataType.ObjectStart:
					return ReadNextObject(ignoreAnyInvalidTypes);
			}
		}

		private int ReadNextInt()
		{
			return BitConverter.ToInt32(ReadNext(sizeof(Int32)), 0);
		}

		private string ReadNextString()
		{
			var length = ReadNextInt();
			return (length == -1) ? null : Encoding.UTF8.GetString(ReadNext(length));
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

			// If the next value is a Reference ID 
			int? referenceID;
			var nextEntryType = ReadNextDataType();
			if (nextEntryType == BinarySerialisationDataType.ReferenceID)
			{
				referenceID = ReadNextInt();
				if (_objectReferences.TryGetValue(referenceID.Value, out var existingReference))
				{
					if (ReadNextDataType() != BinarySerialisationDataType.ObjectEnd)
						throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} was not encountered after reused reference");
					return existingReference;
				}
				nextEntryType = ReadNextDataType();
			}
			else
				referenceID = null;

			// Try to get a reference to the type that we should be deserialising to. If ignoreAnyInvalidTypes is true then don't worry if Type.GetType can't find the type that is
			// specified because we don't care about the return value from this method, we're just parsing the data to progress to the next data that we DO care about.
			var typeIfAvailable = Type.GetType(typeName, throwOnError: !ignoreAnyInvalidTypes);
			var valueIfTypeIsAvailable = (typeIfAvailable == null) ? null : FormatterServices.GetUninitializedObject(typeIfAvailable);
			if ((valueIfTypeIsAvailable != null) && (referenceID != null))
				_objectReferences[referenceID.Value] = valueIfTypeIsAvailable;
			var fieldsSet = new HashSet<Tuple<Type, string>>();
			while (true)
			{
				if (nextEntryType == BinarySerialisationDataType.ObjectEnd)
				{
					if (typeIfAvailable != null)
					{
						foreach (var field in _typeAnalyser.GetAllFieldsThatShouldBeSet(typeIfAvailable))
						{
							if (!fieldsSet.Contains(Tuple.Create(field.DeclaringType, field.Name)))
								throw new FieldNotPresentInSerialisedDataException(field.DeclaringType.AssemblyQualifiedName, field.Name);
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
						_nameReferences[ReadNextInt()] = rawFieldNameInformation;
					}
					else if (nextEntryType == BinarySerialisationDataType.NameReferenceID)
					{
						var nameReferenceID = ReadNextInt();
						if (!_nameReferences.TryGetValue(nameReferenceID, out rawFieldNameInformation))
							throw new ArgumentException("Invalid NameReferenceID: " + nameReferenceID);
					}
					else
						throw new ArgumentException("Unexpected " + nextEntryType + " after FieldName");
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
							fieldsSet.Add(Tuple.Create(field.Member.DeclaringType, field.Member.Name));
						}
					}
					else if (valueIfTypeIsAvailable != null)
					{
						// We successfully deserialised a value but couldn't directly set a field to it - before giving up, there's something else to check.. if an older version
						// of a type was serialised that did have the field that we've got a value for and the target type is a newer version of that type that has a [Deprecated]
						// property that can map the old field onto a new field / property then we should try to set the [Deprecated] property's value to the value that we have.
						// That [Deprecated] property's setter should then set a property / field on the new version of the type. If that is the case, then we can add that new
						// property / field to the have-successfully-set list.
						var (propertySetters, fieldsThatHaveBeenSet) = _typeAnalyser.GetPropertySettersAndFieldsToConsiderToHaveBeenSet(typeIfAvailable, fieldName, typeNameIfRequired, fieldValue?.GetType());
						foreach (var propertySetter in propertySetters)
							propertySetter(valueIfTypeIsAvailable, fieldValue);
						foreach (var fieldThatHasBeenSet in fieldsThatHaveBeenSet)
							fieldsSet.Add(Tuple.Create(fieldThatHasBeenSet.DeclaringType, fieldThatHasBeenSet.Name));
					}
				}
				else
					throw new InvalidOperationException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
				nextEntryType = ReadNextDataType();
			}
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
					_nameReferences[ReadNextInt()] = typeName;
				return typeName;
			}
			else if (nextEntryType == BinarySerialisationDataType.NameReferenceID)
			{
				var nameReferenceID = ReadNextInt();
				if (!_nameReferences.TryGetValue(nameReferenceID, out var typeName))
					throw new ArgumentException("Invalid NameReferenceID: " + nameReferenceID);
				return typeName;
			}
			else
				throw new ArgumentException("Expected String or NameReferenceID for object type name");
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