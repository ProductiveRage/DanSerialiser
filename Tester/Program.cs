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

			var writer = new BinaryWriter();
			serialiser.Serialise(value, writer);

			var reader = new BinaryReader(writer.GetData());
			var clone = reader.Read<int>();

			Console.ReadLine();
		}
	}
}