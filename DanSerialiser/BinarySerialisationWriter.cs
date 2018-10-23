using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DanSerialiser.BinaryTypeStructures;
using DanSerialiser.CachedLookups;
using DanSerialiser.Reflection;
using static DanSerialiser.CachedLookups.BinarySerialisationDeepCompiledMemberSetters;

namespace DanSerialiser
{
	public sealed class BinarySerialisationWriter : IWrite
	{
		private readonly Stream _stream;
		private readonly IAnalyseTypesForSerialisation _typeAnalyser;
		private readonly ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> _deepMemberSetterCacheIfEnabled;
		private readonly Dictionary<Type, BinarySerialisationWriterCachedNames.CachedNameData> _recordedTypeNames;
		private readonly Dictionary<Tuple<FieldInfo, Type>, BinarySerialisationWriterCachedNames.CachedNameData> _encounteredFields;
		private readonly Dictionary<PropertyInfo, BinarySerialisationWriterCachedNames.CachedNameData> _encounteredProperties;
		private readonly Dictionary<Tuple<MemberInfo, Type>, bool> _shouldSerialiseMemberCache;
		private bool _haveStartedSerialising;
		/// <summary>
		/// The default configuration for a BinarySerialisationWriter is to encourage the serialiser to treat the data as a tree-like structure and to traverse each branch to its
		/// end - if the object model has large arrays whose elements are the starts of circular reference chains then this can cause a stack overflow exception. If the value that
		/// you want to serialise sounds like that then setting optimiseForWideCircularReference to true will change how the serialiser approaches the data and should fix the problem
		/// - this alternate approach is more expensive, though (both to serialise and deserialise), and so it is recommended that you only enable it if you have to. Note that the
		/// BinarySerialisationReader constructor does not take this argument, it will be able to tell from the incoming binary data whether the serialiser had
		/// optimiseForWideCircularReference enabled or not.
		/// </summary>
		public BinarySerialisationWriter(Stream stream, bool optimiseForWideCircularReference = false)
			: this(stream, optimiseForWideCircularReference ? ReferenceReuseOptions.OptimiseForWideCircularReferences : ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure, DefaultTypeAnalyser.Instance, null) { }
		internal BinarySerialisationWriter(Stream stream, ReferenceReuseOptions referenceReuseStrategy) : this(stream, referenceReuseStrategy, DefaultTypeAnalyser.Instance, null) { } // internal constructor for unit testing
		internal BinarySerialisationWriter( // internal constructor for unit testing and for FastestTreeBinarySerialisation
			Stream stream,
			ReferenceReuseOptions referenceReuseStrategy,
			IAnalyseTypesForSerialisation typeAnalyser,
			ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> deepMemberSetterCacheIfEnabled)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			ReferenceReuseStrategy = referenceReuseStrategy;
			_typeAnalyser = typeAnalyser;
			_deepMemberSetterCacheIfEnabled = deepMemberSetterCacheIfEnabled; // Only expect this to be non-null when called by FastestTreeBinarySerialisation

			_recordedTypeNames = new Dictionary<Type, BinarySerialisationWriterCachedNames.CachedNameData>();
			_encounteredFields = new Dictionary<Tuple<FieldInfo, Type>, BinarySerialisationWriterCachedNames.CachedNameData>();
			_encounteredProperties = new Dictionary<PropertyInfo, BinarySerialisationWriterCachedNames.CachedNameData>();
			_shouldSerialiseMemberCache = new Dictionary<Tuple<MemberInfo, Type>, bool>();

			_haveStartedSerialising = false;
		}

		public ReferenceReuseOptions ReferenceReuseStrategy { get; }

		public void Boolean(bool value)
		{
			WriteBytes((byte)BinarySerialisationDataType.Boolean, value ? (byte)1 : (byte)0);
		}
		public void Byte(byte value)
		{
			WriteBytes((byte)BinarySerialisationDataType.Byte, value);
		}
		public void SByte(sbyte value)
		{
			WriteBytes((byte)BinarySerialisationDataType.SByte, (byte)value);
		}

		public void Int16(short value)
		{
			WriteBytes((new Int16Bytes(value)).GetLittleEndianBytesWithDataType(BinarySerialisationDataType.Int16));
		}
		public void Int32(int value)
		{
			VariableLengthInt32(value, BinarySerialisationDataType.Int32_8, BinarySerialisationDataType.Int32_16, BinarySerialisationDataType.Int32_24, BinarySerialisationDataType.Int32);
		}
		public void Int64(long value)
		{
			WriteBytes((new Int64Bytes(value)).GetLittleEndianBytesWithDataType());
		}

		public void Single(float value)
		{
			WriteBytes((new SingleBytes(value)).GetLittleEndianBytesWithDataType());
		}
		public void Double(double value)
		{
			WriteBytes((new DoubleBytes(value)).GetLittleEndianBytesWithDataType());
		}
		public void Decimal(decimal value)
		{
			WriteBytes((new DecimalBytes(value)).GetLittleEndianBytesWithDataType());
		}

		public void UInt16(ushort value)
		{
			WriteBytes((new UInt16Bytes(value)).GetLittleEndianBytesWithDataType());
		}
		public void UInt32(uint value)
		{
			WriteBytes((new UInt32Bytes(value)).GetLittleEndianBytesWithDataType());
		}
		public void UInt64(ulong value)
		{
			WriteBytes((new UInt64Bytes(value)).GetLittleEndianBytesWithDataType());
		}

		public void Char(char value)
		{
			WriteBytes((new CharBytes(value)).GetLittleEndianBytesWithDataType());
		}
		public void String(string value)
		{
			WriteByte((byte)BinarySerialisationDataType.String);
			StringWithoutDataType(value);
		}

		// There's nothing inherently special or awkward about DateTime that the Serialiser couldn't serialise it like any other type (by recording all of its fields' values) but it's
		// such a common type that it seems like optimising it a little wouldn't hurt AND having it as an IWrite method means that the BinarySerialisationCompiledMemberSetters can make
		// use of it, which broadens the range of types that it can generate (and that makes things faster when the same types are serialised over and over again)
		public void DateTime(DateTime value)
		{
			// "under the hood a .NET DateTime is essentially a tick count plus a DateTimeKind"
			// - "http://mark-dot-net.blogspot.com/2014/04/roundtrip-serialization-of-datetimes-in.html"
			WriteByte((byte)BinarySerialisationDataType.DateTime);
			WriteBytes((new Int64Bytes(value.Ticks)).GetLittleEndianBytesWithoutDataType());
			WriteByte((byte)value.Kind);
		}

		// TimeSpan is similar to DateTime - the Serialiser could serialise it without special handling but it's a common type and we should be able to do it faster with special code
		// and it will expand the types that SharedGeneratedMemberSetters can deal with
		public void TimeSpan(TimeSpan value)
		{
			WriteByte((byte)BinarySerialisationDataType.TimeSpan);
			WriteBytes((new Int64Bytes(value.Ticks)).GetLittleEndianBytesWithoutDataType());
		}

		// Same again for Guid as for DateTime and TimeSpan (could serialise without but there are benefits to handling it as a special that hopefully outweight the costs of extra code)
		public void Guid(Guid value)
		{
			WriteByte((byte)BinarySerialisationDataType.Guid);
			WriteBytes(value.ToByteArray());
		}

		public void ArrayStart(object value, Type elementType)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (!(value is Array))
				throw new ArgumentException($"If {nameof(value)} is not null then it must be an array");
			if (elementType == null)
				throw new ArgumentNullException(nameof(elementType));

			WriteByte((byte)BinarySerialisationDataType.ArrayStart);
			WriteTypeName(elementType);
			Int32(((Array)value).Length);
		}

		public void ArrayEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ArrayEnd);
		}

		public void Null()
		{
			WriteByte((byte)BinarySerialisationDataType.Null);
		}

		public void ObjectStart(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			WriteByte((byte)BinarySerialisationDataType.ObjectStart);
			WriteTypeName(type);
		}

		public void ObjectEnd()
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectEnd);
		}

		public void ReferenceId(int value)
		{
			VariableLengthInt32(value, BinarySerialisationDataType.ReferenceID8, BinarySerialisationDataType.ReferenceID16, BinarySerialisationDataType.ReferenceID24, BinarySerialisationDataType.ReferenceID32);
		}

		/// <summary>
		/// This indicates that the current object reference being serialised will have its member data written later - when deserialising, an uninitialised instance should be created
		/// that will later have its fields and properties set
		/// </summary>
		public void ObjectContentPostponed()
		{
			WriteByte((byte)BinarySerialisationDataType.ObjectContentPostponed);
		}

		public bool FieldName(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			return WriteFieldNameBytesIfWantoSerialiseField(field, serialisationTargetType);
		}

		/// <summary>
		/// This should only be called when writing out data for deferred-initialised object references - otherwise the boolean return value from the FieldName method will indicate
		/// whether a field should be serialised or not
		/// </summary>
		public bool ShouldSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			var fieldOnType = Tuple.Create((MemberInfo)field, serialisationTargetType);
			if (_shouldSerialiseMemberCache.TryGetValue(fieldOnType, out var cachedData))
				return cachedData;

			cachedData = (BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseField(field, serialisationTargetType) != null);
			_shouldSerialiseMemberCache[fieldOnType] = cachedData;
			return cachedData;
		}

		/// <summary>
		/// This should only be called when writing out data for deferred-initialised object references - otherwise the boolean return value from the FieldName method will indicate
		/// whether a property should be serialised or not
		/// </summary>
		public bool ShouldSerialiseProperty(PropertyInfo property, Type serialisationTargetType)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			var propertyOnType = Tuple.Create((MemberInfo)property, serialisationTargetType);
			if (_shouldSerialiseMemberCache.TryGetValue(propertyOnType, out var cachedData))
				return cachedData;

			cachedData = (BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseProperty(property) != null);
			_shouldSerialiseMemberCache[propertyOnType] = cachedData;
			return cachedData;
		}

		public bool PropertyName(PropertyInfo property, Type serialisationTargetType)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			return WritePropertyNameBytesIfWantoSerialiseField(property);
		}

		/// <summary>
		/// Some writer configurations may perform some upfront analysis based upon the type being serialised. This method should be called by the Serialiser before it starts any
		/// other serialisation process. This will return a dictionary of optimised 'member setters' that the Serialiser may use (a member setter is a delegate that will write the
		/// field and property content for an instance - writing the ObjectStart and ObjectEnd content is the Serialiser's responsibility because that is related to reference-tracking,
		/// which is also the Serialiser's responsibility). Note that this dictionary may include entries that have a null value to indicate that it was not possible generate a member
		/// setter for that type (this may save the Serialiser from calling TryToGenerateMemberSetter for that type later on because it knows that null will be returned). When this
		/// method is called, it will always return a new Dictionary reference and so the caller is free to take ownership of it and mutate it if it wants to. It is not mandatory for
		/// the Serialiser to call this method (and it's not mandatory for it to use the returned dictionary) but it is highly recommended. This should always be called before any
		/// other serialisation methods, calling it after starting serialisation of data will result in an exception being thrown.
		/// </summary>
		public Dictionary<Type, Action<object>> PrepareForSerialisation(Type serialisationTargetType, ISerialisationTypeConverter[] typeConverters)
		{
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));

			if (_haveStartedSerialising)
				throw new Exception(nameof(PrepareForSerialisation) + " must be called before any other serialisation commences");

			// The "BinarySerialisationDeepCompiledMemberSetters.GetMemberSettersFor" method will allow optimised member setters to be generated for more types (which could make
			// the serialisation process much faster) but it may only be used if particular compromises can be made:
			//  - Reference tracking must be disabled (this means that circular references will result in a stack overflow)
			//  - The DefaultTypeAnalyser must be used (because the member setters are cached for reuse and they are generated using the DefaultTypeAnalyser and they may not
			//    be applicable to cases where a different type analyser is required)
			//  - No type converters are used (because these may change the shape of the data during the serialisation process but GetMemberSettersFor needs the data to be remain
			//    in its original form)
			var typeConvertersIsFastTypeConverterArray = (typeConverters is IFastSerialisationTypeConverter[]);
			if ((ReferenceReuseStrategy != ReferenceReuseOptions.SpeedyButLimited)
			|| (_deepMemberSetterCacheIfEnabled == null)
			|| (_typeAnalyser != DefaultTypeAnalyser.Instance)
			|| (!typeConvertersIsFastTypeConverterArray && typeConverters.Any(t => !(t is IFastSerialisationTypeConverter))))
				return new Dictionary<Type, Action<object>>();

			var fastTypeConverters = typeConvertersIsFastTypeConverterArray
				? (IFastSerialisationTypeConverter[])typeConverters
				: typeConverters.Cast<IFastSerialisationTypeConverter>().ToArray();
			var generatedMemberSetterResult = BinarySerialisationDeepCompiledMemberSetters.GetMemberSettersFor(serialisationTargetType, fastTypeConverters, _deepMemberSetterCacheIfEnabled);
			foreach (var typeName in generatedMemberSetterResult.TypeNamesToDeclare)
			{
				WriteByte((byte)BinarySerialisationDataType.TypeNamePreLoad);
				WriteBytes(typeName.AsStringAndReferenceID);
			}
			foreach (var fieldName in generatedMemberSetterResult.FieldNamesToDeclare)
			{
				WriteByte((byte)BinarySerialisationDataType.FieldNamePreLoad);
				WriteBytes(fieldName.AsStringAndReferenceID);
			}
			return generatedMemberSetterResult.MemberSetters.ToDictionary(
				entry => entry.Key,
				entry => (entry.Value == null) ? null : (Action<object>)(source => entry.Value(source, this))
			);
		}

		/// <summary>
		/// This will return a compiled 'member setter' for the specified type, if it's possible to create one. A member setter takes an instance of an object and writes the data
		/// for the fields and properties to the writer. It does not write the ObjectStart and ObjectEnd data since the caller takes responsibility for those because reference
		/// tracking is handled by the caller and it may need to inject a ReferenceID after the ObjectStart data. When reference tracking is enabled, only limited types may have
		/// a member setter generated for them because reference tracking is not possible for the field and property values that the member setter writes (and so the only types
		/// that member setters may be provided for will have fields and properties that are all primitive-like values, such as	genuine primitives and strings and DateTime and
		/// the other types that IWrite handles and that can never result in circular references). This will return null if a member setter could not be provided for the type.
		/// </summary>
		public Action<object> TryToGenerateMemberSetter(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			// The BinarySerialisationCompiledMemberSetters.TryToGenerateMemberSetter method has a facility to take existing member setters and use them for fields or properties
			// on the current type in order to create a more complex member setter - one that can set values other than primitive-esque data types. This would bypass any reference
			// tracking and is not currently enabled (so the "valueWriterRetriever" delegate always returns null). Since reference tracking is not required for struct instances, it
			// may seem reasonable to change this behaviour to allow forming more complex member setters for types whose members are all primitive-like OR structs but structs can
			// have fields that are reference types and reference-tracking IS required for those values and so more analysis would be required in order to be sure that it was safe
			// (structs with reference fields could form part of a circular reference loop and we need to be aware of those, which we can't be if reference tracking is not available).
			var memberSetterAndFieldsSet = BinarySerialisationCompiledMemberSetters.TryToGenerateMemberSetter(type, _typeAnalyser, t => null);
			if (memberSetterAndFieldsSet == null)
				return null;

			var compiledMemberSetter = memberSetterAndFieldsSet.GetCompiledMemberSetter();
			return value => compiledMemberSetter(value, this);
		}

		private void WriteTypeName(Type typeIfValueIsNotNull)
		{
			// When recording a type name, either write a null string for it OR write a string and then the Name Reference ID that that string should be stored as OR write just
			// the Name Reference ID (if the type name has already been recorded once and may be reused)
			if (typeIfValueIsNotNull == null)
			{
				WriteByte((byte)BinarySerialisationDataType.String);
				StringWithoutDataType(null);
				return;
			}

			if (_recordedTypeNames.TryGetValue(typeIfValueIsNotNull, out var cachedData))
			{
				// If we've encountered this field before then we return the bytes for the Name Reference ID only
				WriteBytes(cachedData.OnlyAsReferenceID);
				return;
			}

			// If we haven't encountered this type before then we'll need to write out the full string data (if another write has encountered this type then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this type - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetTypeNameBytes(typeIfValueIsNotNull);
			_recordedTypeNames[typeIfValueIsNotNull] = cachedData;
			WriteBytes(cachedData.AsStringAndReferenceID);
		}

		private bool WriteFieldNameBytesIfWantoSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			var fieldOnType = Tuple.Create(field, serialisationTargetType);
			if (_encounteredFields.TryGetValue(fieldOnType, out var cachedData))
			{
				// If we've encountered this field before then we return the bytes for the Name Reference ID only (unless we've got a null value, which means skip it and return
				// null from here)
				if (cachedData == null)
					return false;

				WriteByte((byte)BinarySerialisationDataType.FieldName);
				WriteBytes(cachedData.OnlyAsReferenceID);
				return true;
			}

			// If we haven't encountered this field before then we'll need to write out the full string data (if another write has encountered this field then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this field - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseField(field, serialisationTargetType);
			_encounteredFields[fieldOnType] = cachedData;
			if (cachedData == null)
				return false;

			WriteByte((byte)BinarySerialisationDataType.FieldName);
			WriteBytes(cachedData.AsStringAndReferenceID);
			return true;
		}

		private bool WritePropertyNameBytesIfWantoSerialiseField(PropertyInfo property)
		{
			if (_encounteredProperties.TryGetValue(property, out var cachedData))
			{
				// If we've encountered this property before then we return the bytes for the Name Reference ID only (unless we've got a null value, which means skip it and
				// return null from here)
				if (cachedData == null)
					return false;

				WriteByte((byte)BinarySerialisationDataType.FieldName);
				WriteBytes(cachedData.OnlyAsReferenceID);
				return true;
			}

			// If we haven't encountered this field before then we'll need to write out the full string data (if another write has encountered this field then the call to the
			// BinarySerialisationWriterCachedNames method should be very cheap but we need to include the string data so that the reader knows what string value to use for
			// this field - if we run into it again in this serialisation process then we'll emit a Name Reference ID and NOT the full string data)
			cachedData = BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseProperty(property);
			_encounteredProperties[property] = cachedData;
			if (cachedData == null)
				return false;

			WriteByte((byte)BinarySerialisationDataType.FieldName);
			WriteBytes(cachedData.AsStringAndReferenceID);
			return true;
		}

		private static readonly int _int24Min = -(int)Math.Pow(2, 23);
		private static readonly int _int24Max = (int)(Math.Pow(2, 23) - 1);
		internal void VariableLengthInt32(int value, BinarySerialisationDataType int8, BinarySerialisationDataType int16, BinarySerialisationDataType int24, BinarySerialisationDataType int32)
		{
			if ((value >= byte.MinValue) && (value <= byte.MaxValue))
				WriteBytes((byte)int8, (byte)value);
			else if ((value >= short.MinValue) && (value <= short.MaxValue))
				WriteBytes((new Int16Bytes((short)value)).GetLittleEndianBytesWithDataType(int16));
			else if ((value >= _int24Min) && (value <= _int24Max))
				WriteBytes((new Int24Bytes(value)).GetLittleEndianBytesWithDataType(int24));
			else
				WriteBytes((new Int32Bytes(value)).GetLittleEndianBytesWithDataType(int32));
		}

		private void StringWithoutDataType(string value)
		{
			if (value == null)
			{
				Int32(-1);
				return;
			}
			if (value == "")
			{
				Int32(0);
				return;
			}

			var bytes = Encoding.UTF8.GetBytes(value);
			Int32(bytes.Length);
			WriteBytes(bytes);
		}

		private void WriteByte(byte value)
		{
			_stream.WriteByte(value);
			_haveStartedSerialising = true;
		}

		private void WriteBytes(byte[] value)
		{
			_stream.Write(value, 0, value.Length);
			_haveStartedSerialising = true;
		}

		// These WriteBytes overloads that take multiple individual bytes are to make it easier to compare calling WriteByte multiple times and wrapping into an array to pass to WriteBytes once
		// (2018-06-30: BenchmarkDotNet seems to report that it's slightly WORSE to call WriteBytes with an array but the flame graph in Code Track indicates a significant speed increase if a
		// single call to WriteByte is used.. I'd like the Benchmark stats to get better but it's only a small decrease shown there and a big increase elsewhere and so I'm going with my gut)
		private void WriteBytes(byte b0, byte b1)
		{
			WriteBytes(new[] { b0, b1 });
		}
	}
}