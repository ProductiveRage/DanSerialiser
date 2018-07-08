using System;
using DanSerialiser;

namespace Tester
{
	public static class Program
	{
		static void Main()
		{
			var value = 32;
			var serialiser = Serialiser.Instance;
			var serialisedData = BinarySerialisation.Serialise(value);
			var clone = BinarySerialisation.Deserialise<int>(serialisedData);
			Console.WriteLine("Cloned value: " + clone);
			Console.ReadLine();
		}
	}
}