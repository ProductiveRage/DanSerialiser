using System;

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

		public object Read(BinarySerialisationReader reader, BinarySerialisationDataType nextEntryType, bool ignoreAnyInvalidTypes)
		{
			if (reader == null)
				throw new ArgumentNullException(nameof(reader));

			var instance = _instantiator();
			foreach (var field in _fields)
			{
				if (nextEntryType != BinarySerialisationDataType.FieldName)
					throw new InvalidOperationException("Unexpected data type encountered while processing fields in BinarySerialisationReaderTypeReader: " + nextEntryType);
				if (reader.ReadNextNameReferenceID(reader.ReadNextDataType()) != field.FieldNameReferenceID)
					throw new Exception($"Fields appeared out of order while being processed by BinarySerialisationReaderTypeReader");

				var value = reader.Read(ignoreAnyInvalidTypes, field.FieldType);
				foreach (var setter in field.Setters)
					setter(instance, value);
				nextEntryType = reader.ReadNextDataType();
			}
			if (nextEntryType != BinarySerialisationDataType.ObjectEnd)
				throw new InvalidOperationException("Unexpected data type encountered after processed fields in BinarySerialisationReaderTypeReader: " + nextEntryType);
			return instance;
		}

		public sealed class FieldSettingDetails
		{
			public FieldSettingDetails(int fieldNameReferenceID, Type fieldType, Action<object, object>[] setters)
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
			public Action<object, object>[] Setters { get; }
		}
	}
}