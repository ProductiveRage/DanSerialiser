using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DanSerialiser.Reflection;

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
				writer,
				writer.SupportReferenceReuse ? null : new object[0],
				writer.SupportReferenceReuse ? new Dictionary<object, int>(ReferenceEqualityComparer.Instance) : null
			);
		}

		private void Serialise(object value, Type type, IWrite writer, IEnumerable<object> parentsIfReferenceReuseDisallowed, Dictionary<object, int> objectHistoryIfReferenceReuseAllowed)
		{
			if ((parentsIfReferenceReuseDisallowed != null) && parentsIfReferenceReuseDisallowed.Contains(value, ReferenceEqualityComparer.Instance))
				throw new CircularReferenceException();

			if (type == typeof(Boolean))
			{
				writer.Boolean((Boolean)value);
				return;
			}
			if (type == typeof(Byte))
			{
				writer.Byte((Byte)value);
				return;
			}
			if (type == typeof(SByte))
			{
				writer.SByte((SByte)value);
				return;
			}

			if (type == typeof(Int16))
			{
				writer.Int16((Int16)value);
				return;
			}
			if (type == typeof(Int32))
			{
				writer.Int32((Int32)value);
				return;
			}
			if (type == typeof(Int64))
			{
				writer.Int64((Int64)value);
				return;
			}

			if (type == typeof(UInt16))
			{
				writer.UInt16((UInt16)value);
				return;
			}
			if (type == typeof(UInt32))
			{
				writer.UInt32((UInt32)value);
				return;
			}
			if (type == typeof(UInt64))
			{
				writer.UInt64((UInt64)value);
				return;
			}

			if (type == typeof(Single))
			{
				writer.Single((Single)value);
				return;
			}
			if (type == typeof(Double))
			{
				writer.Double((Double)value);
				return;
			}
			if (type == typeof(Decimal))
			{
				writer.Decimal((Decimal)value);
				return;
			}

			if (type == typeof(Char))
			{
				writer.Char((Char)value);
				return;
			}
			if (type == typeof(String))
			{
				writer.String((String)value);
				return;
			}

			if (type.IsEnum)
			{
				Serialise(value, type.GetEnumUnderlyingType(), writer, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed);
				return;
			}

			if (type.IsArray)
			{
				var elementType = type.GetElementType();
				writer.ArrayStart(value, elementType);
				if (value != null)
				{
					foreach (var element in (IEnumerable)value)
						Serialise(element, elementType, writer, AppendIfNotNull(parentsIfReferenceReuseDisallowed, value), objectHistoryIfReferenceReuseAllowed);
				}
				writer.ArrayEnd();
				return;
			}

			writer.ObjectStart(value);
			if (value != null)
			{
				bool recordedAsOtherReference;
				if ((objectHistoryIfReferenceReuseAllowed != null) && !type.IsValueType && (type != typeof(string)))
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

		private void SerialiseObjectFieldsAndProperties(object value, Type type, IEnumerable<object> parentsIfReferenceReuseDisallowed, Dictionary<object, int> objectHistoryIfReferenceReuseAllowed, IWrite writer)
		{
			var (fields, properties) = _typeAnalyser.GetFieldsAndProperties(value.GetType());
			foreach (var field in fields)
			{
				if (writer.FieldName(field.Member, type))
					Serialise(field.Reader(value), field.Member.FieldType, writer, AppendIfNotNull(parentsIfReferenceReuseDisallowed, value), objectHistoryIfReferenceReuseAllowed);
			}
			foreach (var property in properties)
			{
				if (writer.PropertyName(property.Member, type))
					Serialise(property.Reader(value), property.Member.PropertyType, writer, AppendIfNotNull(parentsIfReferenceReuseDisallowed, value), objectHistoryIfReferenceReuseAllowed);
			}
		}

		private static IEnumerable<object> AppendIfNotNull(IEnumerable<object> valuesIfAny, object value)
		{
			if (valuesIfAny == null)
				return null;

			return valuesIfAny.Append(value);
		}

		// Courtesy of https://stackoverflow.com/a/41169463/3813189
		private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
		{
			public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}
}