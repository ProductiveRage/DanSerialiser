using System;

namespace DanSerialiser
{
	public interface IWrite : IDisposable
	{
		void Int32(int value);
	}
}