using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DanSerialiser
{
	public sealed class Serialiser
	{
		public static Serialiser Instance { get; } = new Serialiser();
		private Serialiser() { }

		public void Serialise<T>(T value, IWrite writer)
		{
			if (writer == null)
				throw new ArgumentNullException(nameof(writer));

			Serialise(value, typeof(T), writer, new object[0]);
		}

		private void Serialise(object value, Type type, IWrite writer, IEnumerable<object> parents)
		{
			if (parents.Contains(value, ReferenceEqualityComparer.Instance))
				throw new CircularReferenceException();

			if (type == typeof(Int32))
			{
				writer.Int32((Int32)value);
				return;
			}

			if (type == typeof(String))
			{
				writer.String((String)value);
				return;
			}

			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				writer.ListStart(value);
				if (value != null)
				{
					foreach (var element in (IEnumerable)value)
						Serialise(element, type.GetElementType(), writer, parents.Append(value));
				}
				writer.ListEnd();
			}

			writer.ObjectStart(value);
			if (value != null)
			{
				// Track what field names have been used while enumerating them down through the type hierarchy - if there are no ambiguities then we only need to
				// serialise the field names themselves but if names are repeated (if fields are overridden or if they are replaced on derived classes by using
				// "new") then the DeclaringType will have to be serialised as well (the first use of the name won't need the type but subsequent will)
				var fieldNamesUsed = new HashSet<string>();
				var currentTypeToEnumerateMembersFor = value.GetType();
				while (currentTypeToEnumerateMembersFor != null)
				{
					foreach (var field in currentTypeToEnumerateMembersFor.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						var includeTypeName = fieldNamesUsed.Contains(field.Name);
						writer.FieldName(field.Name, includeTypeName ? field.DeclaringType.AssemblyQualifiedName : null);
						Serialise(field.GetValue(value), field.FieldType, writer, parents.Append(value));
						fieldNamesUsed.Add(field.Name);
					}
					currentTypeToEnumerateMembersFor = currentTypeToEnumerateMembersFor.BaseType;
				}
			}
			writer.ObjectEnd();
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