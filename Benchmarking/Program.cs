using System;
using BenchmarkDotNet.Running;

namespace Benchmarking
{
	class Program
	{
		static void Main()
		{
			var summary = BenchmarkRunner.Run<SerialisationPerformance>();
			Console.Write(summary);
		}
	}
}