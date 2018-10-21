using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DanSerialiser
{
	/// <summary>
	/// When the SpecialisationsMayBeIgnoredWhenSerialising attribute affects how a type may be serialised using the FastestTreeBinarySerialisation, any nested abstract type references will
	/// be set to null but it might be important for certain fields or properties to be set non-null even if they are of abstract types - such as the equality comparer within a Dictionary
	/// or HashSet (an instance of these classes can be created without specifying an equality comparer but that will result in the instance using a default implementation internally, if
	/// the deserialiser created an instance of a Dictionary and set its equality comparer to null then any attempts to retrieve entries by key would fail).
	/// 
	/// This serialisation type converter write data for any field or property of type IEqualityComparer&lt;T&gt; to be set to a default implementation using EqualityComparer&lt;T&gt;.Default
	/// so that it is possible to serialise a type with a field that is a Dictionary that is annoted with the SpecialisationsMayBeIgnoredWhenSerialising attribute on it and which should always
	/// use the default equality comparer implementation.
	/// 
	/// You must be aware that if this type converter is used then no other IEqualityComparer&lt;T&gt; implementation will be recorded in the serialised data and so you need to be sure that
	/// this will not break any other data in the content that you wish to serialise. As with the SpecialisationsMayBeIgnoredWhenSerialising attribute, this is not intended for general purpose
	/// serialisation and should only be considered when you are willing to accept some compromises in what data may be serialised in exchange for more serialisation speed.
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
				conversionResult = new FastSerialisationTypeConversionResult(
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
			_serialisationConverters.TryAdd(sourceType, conversionResult); // If another thread did the same work and populated this value, don't worry about it
			return conversionResult;
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