using System;
using DanSerialiser.Reflection;

namespace DanSerialiser.CachedLookups
{
	internal sealed class BinarySerialisationReaderTypeReader
	{
		private readonly Func<object> _instantiator;
		private readonly FieldSettingDetails[] _fields;
		public BinarySerialisationReaderTypeReader(Func<object> instantiator, FieldSettingDetails[] fields)
		{
			_instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
			_fields = fields ?? throw new ArgumentNullException(nameof(fields));
		}

		public object GetUninitialisedInstance()
		{
			return _instantiator();
		}

		public object ReadInto(object instance, BinarySerialisationReader reader, BinarySerialisationDataType nextEntryType, bool ignoreAnyInvalidTypes)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			if (reader == null)
				throw new ArgumentNullException(nameof(reader));

			var numberOfFieldsSet = 0;
			while (numberOfFieldsSet < _fields.Length)
			{
				var field = _fields[numberOfFieldsSet];
				if (nextEntryType != BinarySerialisationDataType.FieldName)
					throw new InvalidSerialisationDataFormatException("Unexpected data type encountered while processing fields in BinarySerialisationReaderTypeReader: " + nextEntryType);

				if (reader.ReadNextNameReferenceID(reader.ReadNextDataType()) != field.FieldNameReferenceID)
				{
					// 2020-12-03 DWR: If this isn't the FieldNameReferenceID that we were expecting next then it must be a field that doesn't exist on the source type in this version of the assembly, suggesting that we're deserialising
					// data from a different version of the type that has this additional field. We want to just skip over this data. We know that we won't end up missing out setting any fields that we DO want on the current version of
					// the entity because the while loop we're in would try to read past the end of the data and it would throw.
					reader.Read(ignoreAnyInvalidTypes: true, targetTypeIfAvailable: null);
				}
				else
				{
					var value = reader.Read(ignoreAnyInvalidTypes, field.FieldType);
					foreach (var setter in field.Setters)
						setter(ref instance, value);
					numberOfFieldsSet++;
				}

				nextEntryType = reader.ReadNextDataType();
			}
			while (nextEntryType == BinarySerialisationDataType.FieldName) // 2020-12-03 DWR: This corresponds with the change above - if there are new properties AFTER all of the expected fields then we need to skip over those as well
			{
				reader.Read(ignoreAnyInvalidTypes: true, targetTypeIfAvailable: null);
				nextEntryType = reader.ReadNextDataType();
			}
			if (nextEntryType != BinarySerialisationDataType.ObjectEnd)
				throw new InvalidSerialisationDataFormatException("Unexpected data type encountered after processed fields in BinarySerialisationReaderTypeReader: " + nextEntryType);
			return instance;
		}

		public sealed class FieldSettingDetails
		{
			public FieldSettingDetails(int fieldNameReferenceID, Type fieldType, MemberUpdater[] setters)
			{
				FieldNameReferenceID = fieldNameReferenceID;
				FieldType = fieldType ?? throw new ArgumentNullException(nameof(fieldType));
				Setters = setters ?? throw new ArgumentNullException(nameof(setters));
			}

			public int FieldNameReferenceID { get; }

			public Type FieldType { get; }

			/// <summary>
			/// If this is a field that is identified as one to be ignored then this will be an empty array, if it is a standard field that needs setting then this will have
			/// one entry and if this field relates to deprecated property/ies in some way then this may have multiple entries
			/// </summary>
			public MemberUpdater[] Setters { get; }
		}
	}
}