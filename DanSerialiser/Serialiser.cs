using System;
using System.Reflection;

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

			Serialise(value, typeof(T), writer);
		}

		private void Serialise(object value, Type type, IWrite writer)
		{
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

			writer.ObjectStart(value);
			if (value != null)
			{
				var currentTypeToEnumerateMembersFor = value.GetType();
				while (currentTypeToEnumerateMembersFor != null)
				{
					foreach (var field in currentTypeToEnumerateMembersFor.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						writer.String(field.Name);
						Serialise(field.GetValue(value), field.FieldType, writer);
					}
					currentTypeToEnumerateMembersFor = currentTypeToEnumerateMembersFor.BaseType;
				}
			}
			writer.ObjectEnd();
		}
	}
}