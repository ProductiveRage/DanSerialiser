using System.Collections.Generic;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class DefaultEqualityComparerFastSerialisationTypeConverterTests
	{
		/// <summary>
		/// By deriving a class from Dictionary that is sealed and by using the DefaultEqualityComparerFastSerialisationTypeConverter, it is possible to serialise a type that
		/// has a Dictionary-like field using only compiled member setters (which provide the best performance). This only works if the dictionary data will never require
		/// a non-default equality comparer. This approach should only be used when trying to eke out the last bit of performance - if this approach is not used then such types
		/// CAN still be serialised, just not via the compiled member setters and so likely a little more slowly (these compiled member setters can only be generated for types
		/// that contain complex nested types, like dictionaries, if reference reuse tracking is disabled).
		/// 
		/// If a consumer of the library wanted to be able to serialise types as quickly as possible that had Dictionary types that would never have non-default equality comparers
		/// but they couldn't change the types to use a SealedDictionary instead of the unsealed framework Dictionary then they could write a type converter that targeted the
		/// Dictionary type and serialised it as a private sealed type, similar to the approach that the DefaultEqualityComparerFastSerialisationTypeConverter takes with
		/// serialising a value for the equality comparer.
		/// </summary>
		[Fact]
		public static void CanSerialiseTypeWithDictionaryIfUseSpecialisationsMayBeIgnoredWhenSerialisingAndDefaultEqualityComparerFastSerialisationTypeConverter()
		{
			var value = new SomethingWithDictionary();
			value.Set(1, "One");

			var clone = BinarySerialisationCloner.Clone(
				value,
				new[] { DefaultEqualityComparerFastSerialisationTypeConverter.Instance },
				new IDeserialisationTypeConverter[0],
				ReferenceReuseOptions.SpeedyButLimited
			);
			Assert.Equal("One", clone.TryToGet(1));
		}

		private sealed class SealedDictionary<TKey, TValue> : Dictionary<TKey, TValue> { }

		private sealed class SomethingWithDictionary
		{
			private readonly SealedDictionary<int, string> _values;
			public SomethingWithDictionary() => _values = new SealedDictionary<int, string>();

			public void Set(int key, string name) => _values[key] = name;
			public string TryToGet(int key) => _values.TryGetValue(key, out var value) ? value : null;
		}
	}
}