using System;
using System.IO;
using DanSerialiser;

namespace Tester
{
	public static class Program
	{
		static void Main()
		{
			var value = 32;

			var serialiser = Serialiser.Instance;

			byte[] serialisedData;
			using (var stream = new MemoryStream())
			{
				var writer = new BinarySerialisationWriter(stream);
				serialiser.Serialise(value, writer);
				serialisedData = stream.ToArray();
			}

			using (var stream = new MemoryStream(serialisedData))
			{
				var reader = new BinarySerialisationReader(stream);
				var clone = reader.Read<int>();
				Console.WriteLine("Cloned value: " + clone);
			}

			Console.ReadLine();
		}
	}
}