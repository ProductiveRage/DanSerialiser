using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	// TODO: Consider moving objectHistoryIfReferenceReuseAllowed to the writer (and having the writer expose a SupportCircularReferences property instead?)
	// - This will make the interface a bit more complicated because it will have to communicate back that it has reused a references and that the properties
	//   do not need to be enumerated and recorded
	public sealed class Serialiser
	{
		public static Serialiser Instance { get; } = new Serialiser(DefaultTypeAnalyser.Instance);

		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		internal Serialiser(IAnalyseTypesForSerialisation typeAnalyser) // internal constructor is intended for unit testing only
		{
			_typeAnalyser = typeAnalyser ?? throw new ArgumentNullException(nameof(typeAnalyser));
		}

		public void Serialise<T>(T value, IWrite writer)
		{
			if (writer == null)
				throw new ArgumentNullException(nameof(writer));

			// We need to know the type that we're serialising and that's why there is a generic type param, so that the caller HAS to specify one even if
			// they're passing null. If we don't have null then take the type from the value argument, otherwise use the type param (we should prefer the
			// value's type because it may be more specific - eg. could call this with a T of object and a value that is a string, in which case we want
			// to process it as a string and not an object).
			Serialise(
				value,
				value?.GetType() ?? typeof(T),
				writer.SupportReferenceReuse ? null : new Stack<object>(),
				writer.SupportReferenceReuse ? new Dictionary<object, int>(ReferenceEqualityComparer.Instance) : null,
				writer
			);
		}

		private void Serialise(object value, Type type, Stack<object> parentsIfReferenceReuseDisallowed, Dictionary<object, int> objectHistoryIfReferenceReuseAllowed, IWrite writer)
		{
			if ((parentsIfReferenceReuseDisallowed != null) && parentsIfReferenceReuseDisallowed.Contains(value, ReferenceEqualityComparer.Instance))
				throw new CircularReferenceException();

			if (type == TypeOfBoolean)
			{
				writer.Boolean((Boolean)value);
				return;
			}
			if (type == TypeOfByte)
			{
				writer.Byte((Byte)value);
				return;
			}
			if (type == TypeOfSByte)
			{
				writer.SByte((SByte)value);
				return;
			}

			if (type == TypeOfInt16)
			{
				writer.Int16((Int16)value);
				return;
			}
			if (type == TypeOfInt32)
			{
				writer.Int32((Int32)value);
				return;
			}
			if (type == TypeOfInt64)
			{
				writer.Int64((Int64)value);
				return;
			}

			if (type == TypeOfUInt16)
			{
				writer.UInt16((UInt16)value);
				return;
			}
			if (type == TypeOfUInt32)
			{
				writer.UInt32((UInt32)value);
				return;
			}
			if (type == TypeOfUInt64)
			{
				writer.UInt64((UInt64)value);
				return;
			}

			if (type == TypeOfSingle)
			{
				writer.Single((Single)value);
				return;
			}
			if (type == TypeOfDouble)
			{
				writer.Double((Double)value);
				return;
			}
			if (type == TypeOfDecimal)
			{
				writer.Decimal((Decimal)value);
				return;
			}

			if (type == TypeOfChar)
			{
				writer.Char((Char)value);
				return;
			}
			if (type == TypeOfString)
			{
				writer.String((String)value);
				return;
			}

			if (type.IsEnum)
			{
				Serialise(value, type.GetEnumUnderlyingType(), parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, writer);
				return;
			}

			if (type.IsArray)
			{
				var elementType = type.GetElementType();
				writer.ArrayStart(value, elementType);
				if (value != null)
				{
					var array = (Array)value;
					for (var i = 0; i < array.Length; i++) // TODO: Need to ensure that de/serialising arrays with multiple dimensions works!
					{
						var element = array.GetValue(i);
						if (parentsIfReferenceReuseDisallowed != null)
							parentsIfReferenceReuseDisallowed.Push(value);
						Serialise(element, elementType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, writer);
						if (parentsIfReferenceReuseDisallowed != null)
							parentsIfReferenceReuseDisallowed.Pop();
					}
				}
				writer.ArrayEnd();
				return;
			}

			writer.ObjectStart(value);
			if (value != null)
			{
				bool recordedAsOtherReference;
				if ((objectHistoryIfReferenceReuseAllowed != null) && !type.IsValueType && (type != TypeOfString))
				{
					if (objectHistoryIfReferenceReuseAllowed.TryGetValue(value, out int referenceID))
						recordedAsOtherReference = true;
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
					}
					writer.ReferenceId(referenceID);
				}
				else
					recordedAsOtherReference = false;
				if (!recordedAsOtherReference)
					SerialiseObjectFieldsAndProperties(value, type, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, writer);
			}
			writer.ObjectEnd();
		}

		private void SerialiseObjectFieldsAndProperties(object value, Type type, Stack<object> parentsIfReferenceReuseDisallowed, Dictionary<object, int> objectHistoryIfReferenceReuseAllowed, IWrite writer)
		{
			// Write out all of the data for the value
			var valueType = value.GetType();
			var (fields, properties) = _typeAnalyser.GetFieldsAndProperties(valueType);
			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				if (writer.FieldName(field.Member, type))
				{
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Push(value);
					Serialise(field.Reader(value), field.Member.FieldType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, writer);
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Pop();
				}
			}
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				if (writer.PropertyName(property.Member, type))
				{
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Push(value);
					Serialise(property.Reader(value), property.Member.PropertyType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, writer);
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Pop();
				}
			}
		}

		// Caching these typeof(..) calls may help performance in some cases, as suggested here:
		//  https://rogerjohansson.blog/2016/08/16/wire-writing-one-of-the-fastest-net-serializers/
		// I saw negligible difference but makes intuitive sense, so I'll leave it in (if only to avoid thinking about it in the future)
		private static readonly Type TypeOfBoolean = typeof(Boolean);
		private static readonly Type TypeOfByte = typeof(Byte);
		private static readonly Type TypeOfSByte = typeof(SByte);
		private static readonly Type TypeOfInt16 = typeof(Int16);
		private static readonly Type TypeOfInt32 = typeof(Int32);
		private static readonly Type TypeOfInt64 = typeof(Int64);
		private static readonly Type TypeOfUInt16 = typeof(UInt16);
		private static readonly Type TypeOfUInt32 = typeof(UInt32);
		private static readonly Type TypeOfUInt64 = typeof(UInt64);
		private static readonly Type TypeOfSingle = typeof(Single);
		private static readonly Type TypeOfDouble = typeof(Double);
		private static readonly Type TypeOfDecimal = typeof(Decimal);
		private static readonly Type TypeOfChar = typeof(Char);
		private static readonly Type TypeOfString = typeof(String);

		// Courtesy of https://stackoverflow.com/a/41169463/3813189
		private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
		{
			public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}
}