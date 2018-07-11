using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DanSerialiser.CachedLookups
{
	/// <summary>
	/// Encoding strings is expensive and so we want to reuse previously-encoded content where possible AND we want to be able to use a Name Reference ID for a type or field
	/// name for the second, third, etc.. time that that name is encountered when performing serialisation. This class contains helper methods (and caches information) to achieve
	/// this (the first time that a type or field name is serialised, the binary data will be cached as will a binary representation of a Name Reference ID so that each Binary
	/// Serialisation Writer instance can reuse pre-encoded strings where possible).
	/// </summary>
	internal static class BinarySerialisationWriterCachedNames
	{
		private static readonly ConcurrentDictionary<Type, CachedNameData> _typeNameCache;
		private static readonly ConcurrentDictionary<Tuple<FieldInfo, Type>, CachedNameData> _fieldNameCache;
		private static readonly ConcurrentDictionary<PropertyInfo, CachedNameData> _propertyNameCache;
		private static int _nextNameReferenceID;
		static BinarySerialisationWriterCachedNames()
		{
			_typeNameCache = new ConcurrentDictionary<Type, CachedNameData>();
			_fieldNameCache = new ConcurrentDictionary<Tuple<FieldInfo, Type>, CachedNameData>();
			_propertyNameCache = new ConcurrentDictionary<PropertyInfo, CachedNameData>();
			_nextNameReferenceID = 0;
		}

		public static CachedNameData GetTypeNameBytes(Type type)
		{
			if (_typeNameCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			var referenceID = Interlocked.Increment(ref _nextNameReferenceID);
			var bytesForReferenceID = GetBytesForNameReferenceID(referenceID);
			var bytesForStringAndReferenceID = new List<byte>();
			using (var stream = new StreamThatAppendsBytesToList(bytesForStringAndReferenceID))
			{
				var writer = new BinarySerialisationWriter(stream);
				writer.String(type.AssemblyQualifiedName);
			}
			bytesForStringAndReferenceID.AddRange(bytesForReferenceID);
			var cacheEntry = new CachedNameData(
				asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
				onlyAsReferenceID: bytesForReferenceID.ToArray()
			);
			_typeNameCache.TryAdd(type, cacheEntry);
			return cacheEntry;
		}

		public static CachedNameData GetFieldNameBytesIfWantoSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			var cacheKey = Tuple.Create(field, serialisationTargetType);
			if (_fieldNameCache.TryGetValue(cacheKey, out var cachedResult))
				return cachedResult;

			if (BinaryReaderWriterShared.IgnoreField(field))
			{
				_fieldNameCache.TryAdd(cacheKey, null);
				return null;
			}

			// Serialisation of pointer fields will fail - I don't know how they would be supportable anyway but they fail with a stack overflow if attempted, so catch it
			// first and raise as a more useful exception
			if (field.FieldType.IsPointer || (field.FieldType == typeof(IntPtr)) || (field.FieldType == typeof(UIntPtr)))
				throw new NotSupportedException($"Can not serialise pointer fields: {field.Name} on {field.DeclaringType.Name}");

			// If a field is declared multiple times in the type hierarchy (whether through overrides or use of "new") then its name will need prefixing with the type
			// that this FieldInfo relates to
			var fieldNameExistsMultipleTimesInHierarchy = false;
			var currentType = serialisationTargetType;
			while (currentType != null)
			{
				if (currentType != serialisationTargetType)
				{
					if (currentType.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags).Any(f => f.Name == field.Name))
					{
						fieldNameExistsMultipleTimesInHierarchy = true;
						break;
					}
				}
				currentType = currentType.BaseType;
			}

			// When recording a field name, either write a string and then the Name Reference ID that that string should be stored as OR write just the Name Reference ID
			// (if the field name has already been recorded once and may be reused)
			var referenceID = Interlocked.Increment(ref _nextNameReferenceID);
			var bytesForReferenceID = GetBytesForNameReferenceID(referenceID);
			var bytesForStringAndReferenceID = new List<byte>();
			using (var stream = new StreamThatAppendsBytesToList(bytesForStringAndReferenceID))
			{
				var writer = new BinarySerialisationWriter(stream);
				writer.String(BinaryReaderWriterShared.CombineTypeAndFieldName(fieldNameExistsMultipleTimesInHierarchy ? field.DeclaringType : null, field.Name));
			}
			bytesForStringAndReferenceID.AddRange(bytesForReferenceID);
			var cacheEntry = new CachedNameData(
				asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
				onlyAsReferenceID: bytesForReferenceID.ToArray()
			);
			_fieldNameCache.TryAdd(cacheKey, cacheEntry);
			return cacheEntry;
		}

		/// <summary>
		/// The BinarySerialisationWriter only records data relating to Deprecated properties and it serialises data for a backing field value, which is why this method is named
		/// Get-FIELD-NAME-bytes-if-want-to-serialise-property (because there is no direct binary representation for the property; it's always field data that is written)
		/// </summary>
		public static CachedNameData GetFieldNameBytesIfWantoSerialiseProperty(PropertyInfo property)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));

			if (_propertyNameCache.TryGetValue(property, out var cachedResult))
				return cachedResult;

			// Most of the time, we'll just serialise the backing fields because that should capture all of the data..
			if (property.GetCustomAttribute<DeprecatedAttribute>() == null)
			{
				_propertyNameCache[property] = null;
				return null;
			}

			if (property.PropertyType.IsPointer || (property.PropertyType == typeof(IntPtr)) || (property.PropertyType == typeof(UIntPtr)))
				throw new NotSupportedException($"Can not serialise pointer properties: {property.Name} on {property.DeclaringType.Name}");

			// .. however, if this is a property that has the [Deprecated] attribute on it then it is expected to exist for backwards compatibility and to be a computed property
			// (and so have no backing field) but one that we want to include in the serialised data anyway. If V1 of a type has a string "Name" property which is replaced in V2
			// with a "TranslatedName" property of type TranslatedString then a computed "Name" property could be added to the V2 type (annotated with [Deprecated]) whose getter
			// returns the default language value of the TranslatedName - this value may then be included in the serialisation data so that an assembly that has loaded the V1
			// type can deserialise and populate its Name property.
			// - Note: We won't try to determine whether or not the type name prefix is necessary when recording the field name because the type hierarchy and the properties on
			//   them might be different now than in the version of the types where deserialisation occurs so the type name will always be inserted before the field name to err
			//   on the safe side
			// - Further note: Similar approach to type and field name recording is taken here; the first time a property is written, the string is serialised, while subsequent
			//   times get a NameReferenceID instead
			var referenceID = Interlocked.Increment(ref _nextNameReferenceID);
			var bytesForReferenceID = GetBytesForNameReferenceID(referenceID);
			var bytesForStringAndReferenceID = new List<byte>();
			using (var stream = new StreamThatAppendsBytesToList(bytesForStringAndReferenceID))
			{
				var writer = new BinarySerialisationWriter(stream);
				writer.String(BinaryReaderWriterShared.CombineTypeAndFieldName(property.DeclaringType, BackingFieldHelpers.GetBackingFieldName(property.Name)));
			}
			bytesForStringAndReferenceID.AddRange(bytesForReferenceID);
			var cacheEntry = new CachedNameData(
				asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
				onlyAsReferenceID: bytesForReferenceID.ToArray()
			);
			_propertyNameCache.TryAdd(property, cacheEntry);
			return cacheEntry;
		}

		private static byte[] GetBytesForNameReferenceID(int referenceID)
		{
			var bytesForReferenceID = new List<byte>();
			using (var stream = new StreamThatAppendsBytesToList(bytesForReferenceID))
			{
				var writer = new BinarySerialisationWriter(stream);
				writer.VariableLengthInt32(
					referenceID,
					BinarySerialisationDataType.NameReferenceID8,
					BinarySerialisationDataType.NameReferenceID16,
					BinarySerialisationDataType.NameReferenceID24,
					BinarySerialisationDataType.NameReferenceID32
				);
			}
			return bytesForReferenceID.ToArray();
		}

		public sealed class CachedNameData
		{
			public CachedNameData(byte[] asStringAndReferenceID, byte[] onlyAsReferenceID)
			{
				AsStringAndReferenceID = asStringAndReferenceID ?? throw new ArgumentNullException(nameof(asStringAndReferenceID));
				OnlyAsReferenceID = onlyAsReferenceID ?? throw new ArgumentNullException(nameof(onlyAsReferenceID));
			}
			public byte[] AsStringAndReferenceID { get; }
			public byte[] OnlyAsReferenceID { get; }
		}

		/// <summary>
		/// This exists so that we can use the writing methods of the BinaryWriter without having to add overloads that write to something other than a stream
		/// </summary>
		private sealed class StreamThatAppendsBytesToList : Stream
		{
			private readonly List<byte> _target;
			public StreamThatAppendsBytesToList(List<byte> target)
			{
				_target = target ?? throw new ArgumentNullException(nameof(target));
			}

			public override bool CanRead => false;
			public override bool CanSeek => false;
			public override bool CanWrite => true;
			public override long Length => _target.Count;
			public override long Position
			{
				get => _target.Count;
				set => throw new NotImplementedException();
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();

			public override void WriteByte(byte value) => _target.Add(value);
			public override void Write(byte[] buffer, int offset, int count)
			{
				if (buffer == null)
					throw new ArgumentNullException(nameof(buffer));

				if ((offset == 0) && (count == buffer.Length))
					_target.AddRange(buffer);
				else
					_target.AddRange(buffer.Skip(offset).Take(count));
			}
		}
	}
}