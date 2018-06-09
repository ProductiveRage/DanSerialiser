using System;
using DanSerialiser;

namespace Tester
{
	public static class Program
	{
		static void Main()
		{
			Console.WriteLine("Hi!");

			var serialiser = new Serialiser();
			serialiser.Serialise(null, NullWriter.Instance);

			Console.ReadLine();
		}
	}
}