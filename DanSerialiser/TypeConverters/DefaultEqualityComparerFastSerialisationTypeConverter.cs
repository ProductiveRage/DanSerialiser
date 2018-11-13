using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DanSerialiser
{
	/// <summary>
	/// In order for the FastestTreeBinarySerialisation to apply as many optimisations as possible, there must not be any 'unknown' types present in the object model - an unknown type is
	/// a non-sealed class or an interface because a property of type non-sealed MyClass MAY have an instance of MyClass when it comes to be serialised or it may have have an instance of
	/// MyOtherClass, which is derived from MyClass. The same applies to interfaces because there is no way to know what type will used to implement that interface. Types that have an
	/// optional IEqualityComparer are common, particularly in the base library - such as Dictionary and HashSet. For instances of those classes, it can not be known during the pre-
	/// serialisation optimisation analysis stage what types may be used for those IEqualityComparer implementations and so some optimisations are not available. However, if the particular
	/// data being serialised will ALWAYS use the default implementation of IEqualityComparer then the type is no longer unknown and the optimisations CAN be applied. This type converter
	/// allows that to be communicated - it should not be used if any IEqualityComparer implementations are anything but the default. This is not intended for general purpose serialisation
	/// and should only be considered when you are willing to accept some compromises in what data may be serialised in exchange for more serialisation speed.
	/// </summary>
	public sealed class DefaultEqualityComparerFastSerialisationTypeConverter : IFastSerialisationTypeConverter
	{
		private static readonly ConcurrentDictionary<Type, FastSerialisationTypeConversionResult> _serialisationConverters = new ConcurrentDictionary<Type, FastSerialisationTypeConversionResult>();

		public static DefaultEqualityComparerFastSerialisationTypeConverter Instance { get; } = new DefaultEqualityComparerFastSerialisationTypeConverter();
		private DefaultEqualityComparerFastSerialisationTypeConverter() { }

		// This type converter should only be used where the IEqualityComparer<T> implementations are expected to use DefaultEqualityComparer<T> and that means that we should never need
		// to considering changing the value when analysing it at serialisation time via the ISerialisationTypeConverter - this class should only impact the pre-serialisation analysis
		object ISerialisationTypeConverter.ConvertIfRequired(object value) => value;

		FastSerialisationTypeConversionResult IFastSerialisationTypeConverter.GetDirectWriterIfPossible(Type sourceType, MemberSetterDetailsRetriever memberSetterDetailsRetriever)
		{
			if (sourceType == null)
				throw new ArgumentNullException(nameof(sourceType));
			if (memberSetterDetailsRetriever == null)
				throw new ArgumentNullException(nameof(memberSetterDetailsRetriever));

			if (!sourceType.IsGenericType)
				return null;
			var genericTypeDefinition = sourceType.GetGenericTypeDefinition();
			if (genericTypeDefinition != typeof(IEqualityComparer<>))
				return null;

			var comparedType = sourceType.GetGenericArguments()[0];
			if (_serialisationConverters.TryGetValue(comparedType, out var conversionResult))
				return conversionResult; // This may be null if the memberSetterDetailsRetriever call below returned null earlier

			var defaultComparerType = typeof(DefaultEqualityComparer<>).MakeGenericType(comparedType);
			var defaultComparerTypeWriter = memberSetterDetailsRetriever(defaultComparerType);
			if (defaultComparerTypeWriter == null)
			{
				// Since the DefaultEqualityComparer class is so simple, I wouldn't expect memberSetterDetailsRetriever to return null but it's
				// possible and so it needs to be handled - the null will be added to the cache dictionary to avoid repeating this work later on
				conversionResult = null;
			}
			else
			{
				var writerParameter = Expression.Parameter(typeof(BinarySerialisationWriter), "writer");
				conversionResult = FastSerialisationTypeConversionResult.ConvertTo(
					type: sourceType,
					convertedToType: defaultComparerType,
					memberSetter: Expression.Lambda(
						Expression.Invoke(
							defaultComparerTypeWriter.MemberSetter,
							Expression.MakeMemberAccess(null, defaultComparerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)),
							writerParameter
						),
						Expression.Parameter(sourceType, "source"),
						writerParameter
					)
				);
			}
			return _serialisationConverters.GetOrAdd(sourceType, conversionResult);
		}

		/// <summary>
		/// This class has the 'Serializable' attribute on it just in case deserialised data that includes this class in it is to be serialised by another serialisation
		/// library (this attibute is not required on the FlattenedImmutableList class in the DefaultEqualityComparerFastSerialisationTypeConverter because instances of
		/// that class do not exist within the deserialised data, it used only during the serialisation and deserialisation processes and nothing of it remains once the
		/// deserialisation process has completed - this type, however, continues to exist in the deserialisation content as the IEqualityComparer implementation, which
		/// is necessary because we can't serialise EqualityComparer&lt;T&gt;Default as EqualityComparer&lt;T&gt; is an abstract class).
		/// </summary>
		[Serializable]
		private sealed class DefaultEqualityComparer<T> : IEqualityComparer<T>
		{
			public static DefaultEqualityComparer<T> Instance { get; } = new DefaultEqualityComparer<T>();
			private DefaultEqualityComparer() { }
			public bool Equals(T x, T y) => EqualityComparer<T>.Default.Equals(x, y);
			public int GetHashCode(T obj) => EqualityComparer<T>.Default.GetHashCode(obj);
		}
	}
}