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
		private static readonly ConcurrentDictionary<(FieldInfo, Type), CachedNameData> _fieldInfoNameCache;
		private static readonly ConcurrentDictionary<string, CachedNameData> _sharedFieldNameCache;
		private static readonly ConcurrentDictionary<PropertyInfo, CachedNameData> _propertyInfoNameCache;
		private static int _nextNameReferenceID;
		static BinarySerialisationWriterCachedNames()
		{
			// 2018-10-07: The type name cache will always take a type name string and map it to a CachedNameData entry - very straight forward. The property name cache does the
			// same thing because the property name strings are a combination of the property name and the declaring type (note that data relating to properties is only required
			// when dealing with [Deprecated] attributes because we can skip the property in all other cases and rely upon the serialisation of whatever field(s) contain the data
			// that the property would return / update). When recording field names, unless there is any ambiguity about what type the field is declared on (which is possible as
			// a field may be declared on a type AND declared on its base type, both times with the same name), only the name of the field is stored (not the name of the declaring
			// type). There is no need to use separate Name Reference IDs for the field "id" on Type A and for the field "id" on Type B, the same Name Reference ID may be used for
			// both - to achieve this, there are two dictionaries; one that maps a particular type-and-field to a CachedNameData instance and another that maps strings to a Cached-
			// NameData instance, to enable this sharing.
			_typeNameCache = new ConcurrentDictionary<Type, CachedNameData>();
			_fieldInfoNameCache = new ConcurrentDictionary<(FieldInfo, Type), CachedNameData>();
			_sharedFieldNameCache = new ConcurrentDictionary<string, CachedNameData>();
			_propertyInfoNameCache = new ConcurrentDictionary<PropertyInfo, CachedNameData>();
			_nextNameReferenceID = 0;
		}

		public static CachedNameData GetTypeNameBytes(Type type)
		{
			if (_typeNameCache.TryGetValue(type, out var cachedResult))
				return cachedResult;

			// Note: It's crucial that we don't allow double-add-to-dictionary-and-return-last-created-CachedNameData-instance behaviour here when multiple threads are getting
			// involved because we need to return consistent CachedNameData ID values for all types
			var referenceID = Interlocked.Increment(ref _nextNameReferenceID);
			var bytesForReferenceID = GetBytesForNameReferenceID(referenceID);
			var bytesForStringAndReferenceID = new List<byte>();
			using (var stream = new StreamThatAppendsBytesToList(bytesForStringAndReferenceID))
			{
				var writer = new BinarySerialisationWriter(stream);
				writer.String(type.AssemblyQualifiedName);
			}
			bytesForStringAndReferenceID.AddRange(bytesForReferenceID);
			return _typeNameCache.GetOrAdd(
				type,
				new CachedNameData(
					id: referenceID,
					asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
					onlyAsReferenceID: bytesForReferenceID.ToArray()
				)
			);
		}

		public static CachedNameData GetFieldNameBytesIfWantoSerialiseField(FieldInfo field, Type serialisationTargetType)
		{
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (serialisationTargetType == null)
				throw new ArgumentNullException(nameof(serialisationTargetType));

			var cacheKey = (field, serialisationTargetType);
			if (_fieldInfoNameCache.TryGetValue(cacheKey, out var cachedResult))
				return cachedResult;

			if (BinaryReaderWriterShared.IgnoreField(field))
				return _fieldInfoNameCache.GetOrAdd(cacheKey, value: null);

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
				if (currentType != field.DeclaringType)
				{
					if (currentType.GetFields(BinaryReaderWriterShared.MemberRetrievalBindingFlags | BindingFlags.DeclaredOnly).Any(f => f.Name == field.Name))
					{
						fieldNameExistsMultipleTimesInHierarchy = true;
						break;
					}
				}
				currentType = currentType.BaseType;
			}

			// When recording a field name, either write a string and then the Name Reference ID that that string should be stored as OR write just the Name Reference ID
			// (if the field name has already been recorded once and may be reused)
			var cacheEntry = _sharedFieldNameCache.GetOrAdd(
				BinaryReaderWriterShared.CombineTypeAndFieldName(fieldNameExistsMultipleTimesInHierarchy ? field.DeclaringType : null, field.Name),
				valueFactory: stringToRecord =>
				{
					// Note: Unlike most other interactions with a ConcurrentDictionary, thread safety is not assured when GetOrAdd is called using the signature that takes a
					// "valueFactory" delegate (the reason for this is that the internal ConcurrentDictionary lock will be released while the delegate is being called because
					// there are no guarantees that whatever user code exists in that delegate won't cause a deadlock somehow). The work that we're doing inside this delegate
					// is not expensive, though, and so it wouldn't be the end of the world if we hit the worst case scenario that meant that this valueFactory delegete would
					// be called twice for the same field name string.
					var referenceID = Interlocked.Increment(ref _nextNameReferenceID);
					var bytesForReferenceID = GetBytesForNameReferenceID(referenceID);
					var bytesForStringAndReferenceID = new List<byte>();
					using (var stream = new StreamThatAppendsBytesToList(bytesForStringAndReferenceID))
					{
						var writer = new BinarySerialisationWriter(stream);
						writer.String(stringToRecord);
					}
					bytesForStringAndReferenceID.AddRange(bytesForReferenceID);
					return new CachedNameData(
						id: referenceID,
						asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
						onlyAsReferenceID: bytesForReferenceID.ToArray()
					);
				}
			);
			return _fieldInfoNameCache.GetOrAdd(cacheKey, cacheEntry);
		}

		/// <summary>
		/// The BinarySerialisationWriter only records data relating to Deprecated properties and it serialises data for a backing field value, which is why this method is named
		/// Get-FIELD-NAME-bytes-if-want-to-serialise-property (because there is no direct binary representation for the property; it's always field data that is written)
		/// </summary>
		public static CachedNameData GetFieldNameBytesIfWantoSerialiseProperty(PropertyInfo property)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));

			if (_propertyInfoNameCache.TryGetValue(property, out var cachedResult))
				return cachedResult;

			// Most of the time, we'll just serialise the backing fields because that should capture all of the data..
			if (property.GetCustomAttribute<DeprecatedAttribute>() == null)
			{
				_propertyInfoNameCache[property] = null;
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
			return _propertyInfoNameCache.GetOrAdd(
				property,
				new CachedNameData(
					id: referenceID,
					asStringAndReferenceID: bytesForStringAndReferenceID.ToArray(),
					onlyAsReferenceID: bytesForReferenceID.ToArray()
				)
			);
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
			public CachedNameData(int id, byte[] asStringAndReferenceID, byte[] onlyAsReferenceID)
			{
				ID = id;
				AsStringAndReferenceID = asStringAndReferenceID ?? throw new ArgumentNullException(nameof(asStringAndReferenceID));
				OnlyAsReferenceID = onlyAsReferenceID ?? throw new ArgumentNullException(nameof(onlyAsReferenceID));
			}
			public int ID { get; }
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