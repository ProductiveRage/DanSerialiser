using System;
using System.Runtime.Serialization;

namespace DanSerialiser
{
	[Serializable]
	public sealed class FieldNotPresentInSerialisedDataException : Exception
	{
		private const string TYPE_NAME = "TypeName";
		private const string FIELD_NAME = "FieldName";
		public FieldNotPresentInSerialisedDataException(string typeName, string fieldName) : base($"Field not found in serialised data - '{fieldName}' for type {typeName}")
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"Null/blank {nameof(fieldName)} specified");

			TypeName = typeName;
			FieldName = fieldName;
		}

		private FieldNotPresentInSerialisedDataException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			TypeName = info.GetString(TYPE_NAME);
			FieldName = info.GetString(FIELD_NAME);
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));

			info.AddValue(TYPE_NAME, TypeName);
			info.AddValue(FIELD_NAME, FieldName);
			base.GetObjectData(info, context);
		}

		public string TypeName { get; }
		public string FieldName { get; }
	}
}