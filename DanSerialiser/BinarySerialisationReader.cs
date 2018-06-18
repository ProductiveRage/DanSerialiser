using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace DanSerialiser
{
	public sealed class BinarySerialisationReader
	{
		private byte[] _data;
		private int _index;
		public BinarySerialisationReader(byte[] data)
		{
			_data = data ?? throw new ArgumentNullException(nameof(data));
			_index = 0;
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
			if (_index >= _data.Length)
				throw new InvalidOperationException("No data to read");

			switch ((BinarySerialisationDataType)ReadNext())
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
			var typeName = ReadNextString();
			if (typeName == null)
			{
				if ((BinarySerialisationDataType)ReadNext() != BinarySerialisationDataType.ObjectEnd)
					throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ObjectEnd)} was not encountered");
				return null;
			}

			// Try to get a reference to the type that we should be deserialising to. If ignoreAnyInvalidTypes is true then don't worry if Type.GetType can't find the type that is
			// specified because we don't care about the return value from this method, we're just parsing the data to progress to the next data that we DO care about.
			var typeIfAvailable = Type.GetType(typeName, throwOnError: !ignoreAnyInvalidTypes);
			var valueIfTypeIsAvailable = (typeIfAvailable == null) ? null : FormatterServices.GetUninitializedObject(typeIfAvailable);
			var fieldsSet = new HashSet<Tuple<Type, string>>();
			while (true)
			{
				var nextEntryType = (BinarySerialisationDataType)ReadNext();
				if (nextEntryType == BinarySerialisationDataType.ObjectEnd)
				{
					if (typeIfAvailable != null)
					{
						var currentType = typeIfAvailable;
						while (currentType != null)
						{
							foreach (var field in currentType.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags))
							{
								if (!BinaryReaderWriterShared.IgnoreField(field)
								&& (field.GetCustomAttribute<OptionalWhenDeserialisingAttribute>() == null)
								&& (BackingFieldHelpers.TryToGetPropertyRelatingToBackingField(field)?.GetCustomAttribute<OptionalWhenDeserialisingAttribute>() == null)
								&& !fieldsSet.Contains(Tuple.Create(field.DeclaringType, field.Name)))
									throw new FieldNotPresentInSerialisedDataException(field.DeclaringType.AssemblyQualifiedName, field.Name);
							}
							currentType = currentType.BaseType;
						}
					}
					return valueIfTypeIsAvailable;
				}
				else if (nextEntryType == BinarySerialisationDataType.FieldName)
				{
					var fieldOrTypeName = ReadNextString();
					string typeNameIfRequired, fieldName;
					if (fieldOrTypeName.StartsWith(BinaryReaderWriterShared.FieldTypeNamePrefix))
					{
						typeNameIfRequired = fieldOrTypeName.Substring(BinaryReaderWriterShared.FieldTypeNamePrefix.Length);
						fieldName = ReadNextString();
					}
					else
					{
						typeNameIfRequired = null;
						fieldName = fieldOrTypeName;
					}

					// Try to get a reference to the field on the target type.. if there is one (if valueIfTypeIsAvailable is null then no-one cases about this data and we're just
					// parsing it to skip over it)
					FieldInfo field;
					if (valueIfTypeIsAvailable == null)
						field = null;
					else
					{
						var typeToLookForMemberOn = valueIfTypeIsAvailable.GetType();
						while (true)
						{
							field = typeToLookForMemberOn.GetField(fieldName, BinaryReaderWriterShared.MemberRetrievalBindingFlags);
							if ((field != null) && ((typeNameIfRequired == null) || (field.DeclaringType.AssemblyQualifiedName == typeNameIfRequired)))
								break;
							typeToLookForMemberOn = typeToLookForMemberOn.BaseType;
							if (typeToLookForMemberOn == null)
								break;
						}
					}

					// Note: If the field doesn't exist then parse the data but don't worry about any types not being available because we're not going to set anything to the value
					// that we get back from the "Read" call (but we still need to parse that data to advance the reader to the next field or the end of the current object)
					var fieldValue = Read(ignoreAnyInvalidTypes: (field == null));

					// Now that we have the value to set the field to IF IT EXISTS, try to set the field.. if it's a field that we've already identified on the type then it's easy.
					// However, it may also have been a field on an older version of the type when it was serialised and now that it's deserialised, we'll need to check for any
					// properties marked with [Deprecated] that we can set with the value that then set the fields that replaced the deprecated field (if this is the case then
					// field will currently be null but valueIfTypeIsAvailable will not be null).
					if (field != null)
					{
						if (!BinaryReaderWriterShared.IgnoreField(field))
						{
							field.SetValue(valueIfTypeIsAvailable, fieldValue);
							fieldsSet.Add(Tuple.Create(field.DeclaringType, field.Name));
						}
					}
					else if (valueIfTypeIsAvailable != null)
					{
						// We successfully deserialised a value but couldn't directly set a field to it - before giving up, there's something else to check.. if an older version
						// of a type was serialised that did have the field that we've got a value for and the target type is a newer version of that type that has a [Deprecated]
						// property that can map the old field onto a new field / property then we should try to set the [Deprecated] property's value to the value that we have.
						// That [Deprecated] property's setter should then set a property / field on the new version of the type. If that is the case, then we can add that new
						// property / field to the have-successfully-set list.
						var propertyName = BackingFieldHelpers.TryToGetNameOfPropertyRelatingToBackingField(fieldName) ?? fieldName;
						var typeToLookForPropertyOn = valueIfTypeIsAvailable.GetType();
						while (typeToLookForPropertyOn != null)
						{
							if ((typeNameIfRequired == null) || (typeToLookForPropertyOn.AssemblyQualifiedName == typeNameIfRequired))
							{
								var deprecatedProperty = typeToLookForPropertyOn.GetProperties(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
									.Where(p => (p.Name == propertyName) && (p.DeclaringType == typeToLookForPropertyOn) && (p.GetIndexParameters().Length == 0) && p.PropertyType.IsAssignableFrom(fieldValue.GetType()))
									.Select(p => new { Property = p, ReplaceBy = p.GetCustomAttribute<DeprecatedAttribute>()?.ReplacedBy })
									.FirstOrDefault(p => p.ReplaceBy != null); // Safe to use FirstOrDefault because there can't be multiple [Deprecated] as AllowMultiple is not set to true on the attribute class
								if (deprecatedProperty != null)
								{
									// Try to find a field that the "ReplacedBy" value relates to (if we find it then we'll consider it to have been set because setting the
									// deprecated property should set it))
									deprecatedProperty.Property.SetValue(valueIfTypeIsAvailable, fieldValue);
									field = typeToLookForPropertyOn.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
										.Where(f => (f.Name == deprecatedProperty.ReplaceBy) && (f.DeclaringType == typeToLookForPropertyOn))
										.FirstOrDefault();
									if (field == null)
									{
										// If the "ReplacedBy" value didn't directly match a field then try to find a property that it matches and then see if there is a
										// backing field for that property that we can set (if we find this then we'll consider to have been set because setting the deprecated
										// property should set it)
										var property = typeToLookForPropertyOn.GetProperties(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
											.Where(p => (p.Name == deprecatedProperty.ReplaceBy) && (p.DeclaringType == typeToLookForPropertyOn) && (p.GetIndexParameters().Length == 0))
											.FirstOrDefault();
										if (property != null)
										{
											var nameOfPotentialBackingFieldForProperty = BackingFieldHelpers.GetBackingFieldName(property.Name);
											field = typeToLookForPropertyOn.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags)
												.Where(f => (f.Name == nameOfPotentialBackingFieldForProperty) && (f.DeclaringType == typeToLookForPropertyOn))
												.FirstOrDefault();
										}
									}
									if (field != null)
									{
										// Although the field hasn't directly been set, it should have been set indirectly by setting the property value above (unless the [Deprecated]
										// "ReplaceBy" value was lying)
										fieldsSet.Add(Tuple.Create(field.DeclaringType, field.Name));
									}
								}
							}
							typeToLookForPropertyOn = typeToLookForPropertyOn.BaseType;
						}
					}
				}
				else
					throw new InvalidOperationException("Unexpected data type encountered while enumerating object properties: " + nextEntryType);
			}
		}

		private object ReadNextArray(bool ignoreAnyInvalidTypes)
		{
			var elementTypeName = ReadNextString();
			if (elementTypeName == null)
			{
				// If the element type was recorded as null then it means that the array itself was null (and so the next character should be an ArrayEnd)
				if ((BinarySerialisationDataType)ReadNext() != BinarySerialisationDataType.ArrayEnd)
					throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ArrayEnd)} was not encountered");
				return null;
			}
			var elementType = Type.GetType(elementTypeName, throwOnError: true);
			var items = Array.CreateInstance(elementType, length: ReadNextInt());
			for (var i = 0; i < items.Length; i++)
				items.SetValue(Read(ignoreAnyInvalidTypes), i);
			var nextEntryType = (BinarySerialisationDataType)ReadNext();
			if (nextEntryType != BinarySerialisationDataType.ArrayEnd)
				throw new InvalidOperationException($"Expected {nameof(BinarySerialisationDataType.ArrayEnd)} was not encountered");
			return items;
		}

		private byte ReadNext()
		{
			return ReadNext(1)[0];
		}

		private byte[] ReadNext(int numberOfBytes)
		{
			if (_index + numberOfBytes > _data.Length)
				throw new InvalidOperationException("Insufficient data to read (presume invalid content)");

			var values = new byte[numberOfBytes];
			Array.Copy(_data, _index, values, 0, numberOfBytes);
			_index += numberOfBytes;
			return values;
		}
	}
}