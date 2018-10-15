using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DanSerialiser
{
	/// <summary>
	/// It is common for immutable lists to be represented internally as linked lists, which the serialiser CAN process but which may result in stack overflow exceptions
	/// if the list is too large (because the list will be processed recursively and each item in the list will be another frame on the call stack). This type converter
	/// will serialise types that look like immutable lists as arrays and then populate an instance the original type during deserialisation - in order to 'look like'
	/// an immutable list, a type must have a single generic type parameter, it must implement IEnumerable of that type parameter, it must have a public static 'Empty'
	/// field or property that returns an instance of itself, it must have either an 'AddRange' or an 'InsertRange' public instance method that takes an IEnumerable of
	/// its generic type parameter and which returns an instance of itself AND it must have precisely one private instance field. The reason for one-private-instance-field
	/// restrictions is to try to ensure that this type converter does not get incorrectly applied to other, more specialised list types - for example, it would be easy to
	/// write a variation on the ImmutableHashSet that contained a list of items and an equality comparer; without the restriction, this would get serialised as an array
	/// and then deserialised from that array and the equality comparer would get lost (System.Collections.Immutable.ImmutableHashSet does not have AddRange or InsertRange
	/// methods and so would not be considered by this type converter but this type converter is not focused specifical on the System.Collections.Immutable classes).
	/// 
	/// Due to the cautious nature of this type converter (due the described restrictions), it should be safe to use in any scenario. However, it exists as a separate type
	/// converter (rather than being built in and enabled at all times) so that a consumer of this library is aware that immutable types MAY need extra attention. Additional
	/// type converter(s) would be required for other immutable types, such as the ImmutableHashSet or ImmutableDictionary, if they use linked list structures internally and
	/// if instances of those types have large numbers of items (where 'large' is relative to how deep the call stack can be).
	/// </summary>
	public sealed class ImmutableListTypeConverter : ISerialisationTypeConverter, IDeserialisationTypeConverter
	{
		private static readonly ConcurrentDictionary<Type, ListSerialiser> _serialisationConverters = new ConcurrentDictionary<Type, ListSerialiser>();

		public static ImmutableListTypeConverter Instance { get; } = new ImmutableListTypeConverter();
		private ImmutableListTypeConverter() { }

		object ISerialisationTypeConverter.ConvertIfRequired(object value)
		{
			// Avoid the dictionary lookup completely if we know that the value is null or it's not a generic type with a single type param (not only do we avoid the
			// lookup but we avoid stuffing entries in there for types that could never be applicable - no point recording a "not applicable" entry for Int32)
			if (value == null)
				return null;
			var type = value.GetType();
			if (!type.IsGenericType || (type.GetGenericArguments().Length != 1))
				return value;

			var serialiser = TryToGetListSerialiserFromCache(type);
			return (serialiser == null) ? value : serialiser.Serialise.Value(value);
		}

		object IDeserialisationTypeConverter.ConvertIfRequired(Type targetType, object value)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			// Avoid the dictionary lookup entirely if possible (if we don't have an array to convert back or the target type is definitely not the correct shape
			// then none of this logic will apply)
			if ((value == null) || !value.GetType().IsArray || !targetType.IsGenericType || (targetType.GetGenericArguments().Length != 1))
				return value;

			var serialiser = TryToGetListSerialiserFromCache(targetType);
			return (serialiser == null) ? value : serialiser.Deserialise.Value(value);
		}

		/// <summary>
		/// This will return null if the specified type is not applicable to this type converter
		/// </summary>
		private static ListSerialiser TryToGetListSerialiserFromCache(Type genericType)
		{
			if (_serialisationConverters.TryGetValue(genericType, out var serialiser))
				return serialiser;

			serialiser = TryToGetListSerialiser(genericType);
			_serialisationConverters.TryAdd(genericType, serialiser);
			return serialiser;
		}

		/// <summary>
		/// This will return null if the specified type is not applicable to this type converter
		/// </summary>
		private static ListSerialiser TryToGetListSerialiser(Type type)
		{
			if (!type.IsGenericType)
				return null;
			var genericArgs = type.GetGenericArguments();
			if (genericArgs.Length != 1)
				return null;

			var elementType = genericArgs[0];
			var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
			if (!enumerableType.IsAssignableFrom(type))
				return null;

			MemberInfo emptyInstanceMember;
			var emptyInstanceProperty = type.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static, null, type, Type.EmptyTypes, null);
			if (emptyInstanceProperty != null)
			{
				if (!emptyInstanceProperty.CanRead || emptyInstanceProperty.PropertyType != type)
					return null;
				emptyInstanceMember = emptyInstanceProperty;
			}
			else
			{
				var emptyInstanceField = type.GetField("Empty", BindingFlags.Public | BindingFlags.Static);
				if (emptyInstanceField?.FieldType != type)
					return null;
				emptyInstanceMember = emptyInstanceField;
			}

			var addRangeMethod = type.GetMethod("AddRange", BindingFlags.Public | BindingFlags.Instance, null, new[] { enumerableType }, null);
			if (addRangeMethod?.ReturnType != type)
			{
				// If there is no "AddRange" method we can use, check for an "InsertRange" because they will perform the same for an empty list
				addRangeMethod = type.GetMethod("InsertRange", BindingFlags.Public | BindingFlags.Instance, null, new[] { enumerableType }, null);
				if (addRangeMethod?.ReturnType != type)
					return null;
			}

			// The Func<object, object> delegates for transforming-when-serialising and transforming-when-deserialising are defined within Lazy instances here so
			// that the cost of generating them need not be paid until they are required - if the current process ONLY serialises these types or ONLY deserialises
			// them then it will be able to generate the delegate that it needs and not generate the one that it doesn't.
			var serialise = new Lazy<Func<object, object>>(() =>
			{
				var sourceParameter = Expression.Parameter(typeof(object), "source");
				Expression toArray;
				var toArrayInstanceMethodIfAny = type.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (toArrayInstanceMethodIfAny?.ReturnType == elementType.MakeArrayType())
				{
					// If the type has its own "ToArray" method then use that (it may know something that we don't and have some nice optimisations)
					toArray = Expression.Call(Expression.Convert(sourceParameter, type), toArrayInstanceMethodIfAny);
				}
				else
				{
					// If the type DOESN'T have its own "ToArray" method then use the static one within this class (I considered using LINQ's "ToArray" method
					// in the static System.Linq.Enumerable class but it seemed like something outside of my control that I'm going to access via reflection,
					// which is an inherently risky behaviour - while it's very unlikle that anything will change with it in the future, I still feel better
					// this way)
					var staticToArrayMethod = typeof(ImmutableListTypeConverter).GetMethod(nameof(ToArray), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(elementType);
					toArray = Expression.Call(null, staticToArrayMethod, Expression.Convert(sourceParameter, enumerableType));
				}
				return Expression.Lambda<Func<object, object>>(toArray, sourceParameter).Compile();
			});
			var deserialise = new Lazy<Func<object, object>>(() =>
			{
				var sourceParameter = Expression.Parameter(typeof(object), "source");
				var rebuildList = Expression.Condition(
					Expression.TypeIs(sourceParameter, enumerableType),
					Expression.Convert(
						Expression.Call(
							Expression.MakeMemberAccess(null, emptyInstanceMember),
							addRangeMethod,
							Expression.Convert(sourceParameter, enumerableType)
						),
						typeof(object)
					),
					sourceParameter
				);
				return Expression.Lambda<Func<object, object>>(rebuildList, sourceParameter).Compile();
			});
			return new ListSerialiser(serialise, deserialise);
		}

		private static T[] ToArray<T>(IEnumerable<T> value) => value?.ToArray();

		private sealed class ListSerialiser
		{
			public ListSerialiser(Lazy<Func<object, object>> serialise, Lazy<Func<object, object>> deserialise)
			{
				Serialise = serialise ?? throw new ArgumentNullException(nameof(serialise));
				Deserialise = deserialise ?? throw new ArgumentNullException(nameof(deserialise));
			}
			public Lazy<Func<object, object>> Serialise { get; }
			public Lazy<Func<object, object>> Deserialise { get; }
		}
	}
}