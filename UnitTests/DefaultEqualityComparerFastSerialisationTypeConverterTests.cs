using System.Collections.Generic;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class DefaultEqualityComparerFastSerialisationTypeConverterTests
	{
		/// <summary>
		/// By combining the SpecialisationsMayBeIgnoredWhenSerialising attribute and the DefaultEqualityComparerFastSerialisationTypeConverter, it is possible to serialise
		/// a type that has a Dictionary field using only compiled member setters (which provide the best performance). This only works if the Dictionary will never require
		/// a non-default equality comparer. This approach should only be used when trying to eke out the last bit of performance - if these attributes are not used then
		/// such types CAN still be serialised, just not via the compiled member setters and so likely a little more slowly (these compiled member setters can only be
		/// generated for types that contain complex nested types, like dictionaries, if reference reuse tracking is disabled).
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

		private sealed class SomethingWithDictionary
		{
			[SpecialisationsMayBeIgnoredWhenSerialising]
			private readonly Dictionary<int, string> _values;
			public SomethingWithDictionary() => _values = new Dictionary<int, string>();

			public void Set(int key, string name) => _values[key] = name;
			public string TryToGet(int key) => _values.TryGetValue(key, out var value) ? value : null;
		}
	}
}