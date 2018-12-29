using System;
using System.Collections.Concurrent;
using System.IO;
using DanSerialiser.CachedLookups;
using DanSerialiser.Reflection;
using static DanSerialiser.CachedLookups.BinarySerialisationDeepCompiledMemberSetters;

namespace DanSerialiser
{
	public static class FastestTreeBinarySerialisation
	{
		private static readonly IOptimisingSerialiser NoTypeConverterSerialiser = new OptimisingSerialiser(
			new ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults>(),
			new IFastSerialisationTypeConverter[0]
		);

		/// <summary>
		/// This uses the BinarySerialisationWriter in a configuration that disables reference reuse and circular reference tracking, which enables additional optimisations to be made to the
		/// process for faster serialisation. This serialisation method should not be used with data in which the same references appear multiple times - if there are any circular references
		/// then a stack overflow exception will be thrown and if the same references are repeated many times within the data then the standard Binary Serialisation Writer may provide the best
		/// performance - but this may offer the best serialisation performance if your data structure meets these requirements. It still has support for the DanSerialiser attributes that affect
		/// entity versioning (such as Deprecated and OptionalWhenDeserialising) and its output may still be consumed by the BinarySerialisationReader. Note: Using sealed classes wherever possible
		/// allows this serialisation process to enable more optimisations in some cases and so that is a recommended practice if you want the fastest results.
		/// </summary>
		public static IOptimisingSerialiser GetSerialiser() => NoTypeConverterSerialiser;

		/// <summary>
		/// This uses the BinarySerialisationWriter in a configuration that disables reference reuse and circular reference tracking, which enables additional optimisations to be made to the
		/// process for faster serialisation. Not only is it limited in terms of reference tracking but it also requires more specialised type converters. This serialisation method should not be
		/// used with data in which the same references appear multiple times - if there are any circular references then a stack overflow exception will be thrown and if the same references are
		/// repeated many times within the data then the standard Binary Serialisation Writer may provide the best performance - but this may offer the best serialisation performance if your data
		/// structure meets these requirements. It still has support for the DanSerialiser attributes that affect entity versioning (such as Deprecated and OptionalWhenDeserialising) and its output
		/// may still be consumed by the BinarySerialisationReader. Note: Using sealed classes wherever possible allows this serialisation process to enable more optimisations in some cases and so
		/// that is a recommended practice if you want the fastest results.
		/// 
		/// IMPORTANT: The serialiser that is returned from this method should be reused for repeated serialisations so that the work to identify optimisations available when serialisation a particular
		/// type are shared between one serialisation and the next. This is not necessary for cases where zero type converters are specified because the library will track and automatically reuse the
		/// zero-type-converter optimised serialiser but if any type converters are required then then the resulting serialiser must be reused by the application code. It is safe to share the serialiser
		/// across multiple threads for concurrent use.
		/// </summary>
		public static IOptimisingSerialiser GetSerialiser(IFastSerialisationTypeConverter[] typeConverters)
		{
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));

			if (typeConverters.Length == 0)
				return NoTypeConverterSerialiser;

			// Since the serialiser reference should be kept around and reused (so that the Type:DeepCompiledMemberSettersGenerationResults dictionary is maintained across serialisations), it
			// makes sense to be a bit cautious and to clone the type converters array first - things could get very unpredictable if the caller of this method started mutating things in the
			// type converter array after some of the optimisation analysis had started!
			var clonedTypeConverters = new IFastSerialisationTypeConverter[typeConverters.Length];
			for (var i = 0; i < typeConverters.Length; i++)
			{
				if (typeConverters[i] == null)
					throw new ArgumentException("Null reference encountered in array", nameof(typeConverters));
				clonedTypeConverters[i] = typeConverters[i];
			}
			return new OptimisingSerialiser(
				new ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults>(),
				clonedTypeConverters
			);
		}

		/// <summary>
		/// This will throw a FastestTreeSerialisationNotPossibleException if the specified type does not fully support FastestTreeBinarySerialisation - this may be because
		/// the type is a class that is not sealed or it has a member that is a class that is not sealed or if IntPtr values exist within the object graph (which are not
		/// supported by this serialiser). It will only throw at the first problem because one problem can have knock on effects and so a single property of an unsupported
		/// property type that is deeply nested in the object graph will prevent all of the parent types getting the full-speed treatment.
		/// 
		/// This method is intended for us in unit tests in code that references this library - if it is important for this fastest possible serialisation approach to be
		/// used for particular types then it would be sensible to have unit tests that catch any changes to those types that would prevent optimal serialisation performance
		/// because that change is likely to result in a performance regression.
		/// </summary>
		public static void EnsureThatTypeIsOptimalForFastestTreeSerialisation(Type type, IFastSerialisationTypeConverter[] typeConverters)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (typeConverters == null)
				throw new ArgumentNullException(nameof(typeConverters));

			// If this method doesn't throw, then great! If it DOES throw then let that exception bubble up to the caller to let them know that all is not well.
			BinarySerialisationDeepCompiledMemberSetters.EnsureThatTypeIsOptimalForFastestTreeSerialisation(type, typeConverters);
		}

		public interface IOptimisingSerialiser
		{
			byte[] Serialise(object value);
			void Serialise(object value, Stream stream);
		}

		private sealed class OptimisingSerialiser : IOptimisingSerialiser
		{
			private readonly IFastSerialisationTypeConverter[] _typeConverters;
			private readonly ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> _deepMemberSetterCache;
			public OptimisingSerialiser(ConcurrentDictionary<Type, DeepCompiledMemberSettersGenerationResults> deepMemberSetterCache, IFastSerialisationTypeConverter[] typeConverters)
			{
				_deepMemberSetterCache = deepMemberSetterCache ?? throw new ArgumentNullException(nameof(deepMemberSetterCache));
				_typeConverters = typeConverters ?? throw new ArgumentNullException(nameof(typeConverters));
			}

			public byte[] Serialise(object value)
			{
				using (var stream = new MemoryStream())
				{
					Serialise(value, stream);
					return stream.ToArray();
				}
			}

			public void Serialise(object value, Stream stream)
			{
				if (stream == null)
					throw new ArgumentNullException(nameof(stream));

				Serialiser.Instance.Serialise(
					value,
					_typeConverters,
					new BinarySerialisationWriter(stream, ReferenceReuseOptions.SpeedyButLimited, DefaultTypeAnalyser.Instance, _deepMemberSetterCache)
				);
			}
		}
	}
}