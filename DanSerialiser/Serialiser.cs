using System;

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

			if (typeof(T) == typeof(Int32))
			{
				writer.Int32((Int32)(object)value);
				return;
			}

			if (typeof(T) == typeof(String))
			{
				writer.String((String)(object)value);
				return;
			}

			writer.ObjectStart(value);
			writer.ObjectEnd();
		}
	}
}