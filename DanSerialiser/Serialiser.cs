using System;

namespace DanSerialiser
{
	public sealed class Serialiser
	{
		public Serialiser()
		{
		}

		public void Serialise(object value, IWrite writer)
		{
			if (writer == null)
				throw new ArgumentNullException(nameof(writer));

			throw new NotImplementedException();
		}
	}
}