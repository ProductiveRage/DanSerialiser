using System;
using DanSerialiser.Exceptions;
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

			foreach (var field in _fields)
			{
				if (nextEntryType != BinarySerialisationDataType.FieldName)
					throw new InvalidSerialisationDataFormatException("Unexpected data type encountered while processing fields in BinarySerialisationReaderTypeReader: " + nextEntryType);
				if (reader.ReadNextNameReferenceID(reader.ReadNextDataType()) != field.FieldNameReferenceID)
					throw new InvalidSerialisationDataFormatException($"Fields appeared out of order while being processed by BinarySerialisationReaderTypeReader");

				var value = reader.Read(ignoreAnyInvalidTypes, field.FieldType);
				foreach (var setter in field.Setters)
					setter(ref instance, value);
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