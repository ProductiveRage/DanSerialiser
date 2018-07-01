using System;
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
				writer.SupportReferenceReuse ? null : new Stack<object>(),
				writer.SupportReferenceReuse ? new Dictionary<object, int>(ReferenceEqualityComparer.Instance) : null,
				new Dictionary<Type, Action<object>>(),
				writer
			);
		}

		private void Serialise(
			object value,
			Type type,
			Stack<object> parentsIfReferenceReuseDisallowed,
			Dictionary<object, int> objectHistoryIfReferenceReuseAllowed,
			Dictionary<Type, Action<object>> generatedMemberSetters,
			IWrite writer)
		{
			if ((parentsIfReferenceReuseDisallowed != null) && parentsIfReferenceReuseDisallowed.Contains(value, ReferenceEqualityComparer.Instance))
				throw new CircularReferenceException();

			// If the we've got a Nullable<> then unpack the internal value/type - if it's null then we'll get a null ObjectStart/ObjectEnd value which the BinarySerialisationReader
			// will happily interpret (reading it as a null and setting the Nullable<> field) and if it's non-null then we'll serialise just the value itself (again, the reader will
			// take that value and happily set a Nullable<> field - so if we write an int then the reader will read the int and set the int? field just fine). Doing this means that
			// we have less work to do (otherwise we'd record the Nullable<> as an object and write the type name and so it's just more work to write, more work to read and takes
			// up more data in the serialisation output).
			if ((value != null) && type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
				type = type.GetGenericArguments()[0];

			if (type == CommonTypeOfs.Boolean)
			{
				writer.Boolean((Boolean)value);
				return;
			}
			if (type == CommonTypeOfs.Byte)
			{
				writer.Byte((Byte)value);
				return;
			}
			if (type == CommonTypeOfs.SByte)
			{
				writer.SByte((SByte)value);
				return;
			}

			if (type == CommonTypeOfs.Int16)
			{
				writer.Int16((Int16)value);
				return;
			}
			if (type == CommonTypeOfs.Int32)
			{
				writer.Int32((Int32)value);
				return;
			}
			if (type == CommonTypeOfs.Int64)
			{
				writer.Int64((Int64)value);
				return;
			}

			if (type == CommonTypeOfs.UInt16)
			{
				writer.UInt16((UInt16)value);
				return;
			}
			if (type == CommonTypeOfs.UInt32)
			{
				writer.UInt32((UInt32)value);
				return;
			}
			if (type == CommonTypeOfs.UInt64)
			{
				writer.UInt64((UInt64)value);
				return;
			}

			if (type == CommonTypeOfs.Single)
			{
				writer.Single((Single)value);
				return;
			}
			if (type == CommonTypeOfs.Double)
			{
				writer.Double((Double)value);
				return;
			}
			if (type == CommonTypeOfs.Decimal)
			{
				writer.Decimal((Decimal)value);
				return;
			}

			if (type == CommonTypeOfs.Char)
			{
				writer.Char((Char)value);
				return;
			}
			if (type == CommonTypeOfs.String)
			{
				if (value == null)
					writer.Null();
				else
					writer.String((String)value);
				return;
			}

			if (type == CommonTypeOfs.DateTime)
			{
				writer.DateTime((DateTime)value);
				return;
			}

			// For Object and Array types, if we've got a null reference then write a Null value (having this null check here avoids the type.IsEnum and type.IsArray checks
			// for cases where we DO have a null reference(
			if (value == null)
			{
				writer.Null();
				return;
			}

			if (type.IsEnum)
			{
				Serialise(value, type.GetEnumUnderlyingType(), parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, generatedMemberSetters, writer);
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
						Serialise(element, elementType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, generatedMemberSetters, writer);
						if (parentsIfReferenceReuseDisallowed != null)
							parentsIfReferenceReuseDisallowed.Pop();
					}
				}
				writer.ArrayEnd();
				return;
			}

			writer.ObjectStart(value);
			bool recordedAsOtherReference;
			if ((objectHistoryIfReferenceReuseAllowed != null) && (type != CommonTypeOfs.String) && !type.IsValueType)
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
				SerialiseObjectFieldsAndProperties(value, type, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, generatedMemberSetters, writer);
			writer.ObjectEnd();
		}

		private void SerialiseObjectFieldsAndProperties(
			object value,
			Type type,
			Stack<object> parentsIfReferenceReuseDisallowed,
			Dictionary<object, int> objectHistoryIfReferenceReuseAllowed,
			Dictionary<Type, Action<object>> generatedMemberSetters,
			IWrite writer)
		{
			// It may be possible for a "type generator" to be created for some types (generally simple types that won't require any nested Serialise calls that involve tracking
			// parentsIfReferenceReuseDisallowed or objectHistoryIfReferenceReuseAllowed), so check that first. There are three cases; 1. we don't have any type generator data
			// about the current type, 2. we have tried to retrieve a type generator before and got back null (meaning that this type does not match the writer's conditions
			// for being able to create a type generator) and 3. we have successfully created a type generator before. If it's case 3 then we'll use that type generator
			// instead of enumerating fields below but if it's case 1 or 2 then we'll have to do that work (but if it's case 1 then we'll try to find out whether it's
			// possible to create a type generator at the bottom of this method).
			var valueType = value.GetType();
			var haveTriedToGenerateMemberSetterBefore = generatedMemberSetters.TryGetValue(type, out var memberSetter);
			if (haveTriedToGenerateMemberSetterBefore && (memberSetter != null))
			{
				memberSetter(value);
				return;
			}

			// Write out all of the data for the value
			var (fields, properties) = _typeAnalyser.GetFieldsAndProperties(valueType);
			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				if (writer.FieldName(field.Member, type))
				{
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Push(value);
					Serialise(field.Reader(value), field.Member.FieldType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, generatedMemberSetters, writer);
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
					Serialise(property.Reader(value), property.Member.PropertyType, parentsIfReferenceReuseDisallowed, objectHistoryIfReferenceReuseAllowed, generatedMemberSetters, writer);
					if (parentsIfReferenceReuseDisallowed != null)
						parentsIfReferenceReuseDisallowed.Pop();
				}
			}

			// If we have tried before to create a type generator for this type and were unsuccessful then there is nothing more to do..
			if (haveTriedToGenerateMemberSetterBefore)
				return;

			// .. but if we HAVEN'T tried to create a type generator before then ask the writer if it's able to do so (this is done after the first time that an instance of
			// the type has been fully serialised so that the writer has a chance to create any Name Reference IDs that it might want to use for the member names and potentially
			// have done some other forms of caching)
			generatedMemberSetters[type] = writer.TryToGenerateMemberSetter(type);
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