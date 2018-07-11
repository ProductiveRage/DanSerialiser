using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Running;
using Benchmarking.Entities;
using DanSerialiser;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Benchmarking
{
	/// <summary>
	/// Performance isn't the number one goal for this serialiser but it's still important and so these tests compare it to the BinaryFormatter, ProtoBuf-Net and Json.NET (the first two - and this serialiser -
	/// use a binary format while Json.NET serialises, of course, to json). The entities all have the [Serializable] attribute on them (for the BinaryFormatter) and have public mutable properties (which is
	/// easiest for ProtoBuf-Net and Json.NET) and so some of the other features of this library are not exercised - but if more features were used then it might not be possible to use other serialisers in
	/// order to compare their performance!
	/// 
	/// BenchmarkDotNet supports running the tests against multiple frameworks (such as netcoreapp2.1 and net461) - when the Benchmarking project is run, it will emit assemblies built in the different frameworks
	/// and execute them before aggregating the results. So it is not important to run the Benchmarking project itself in multiple frameworks, it only needs to be run in one and then the performance tests will
	/// be run against each target framework (so calling 'dotnet run -c release --framework netcoreapp2.1' from the command line will result in the test being run in all target frameworks, despite it looking
	/// like they might only be run in .NET Core).
	/// </summary>
	[CoreJob, ClrJob]
	public class SerialisationPerformance
	{
		private Product[] _products;
		private string _jsonNetSerialisedData;
		private byte[] _binaryFormatterSerialisedData, _protoBufSerialisedData, _danSerialiserSerialisedData, _danSerialiserSerialisedDataOptimisedForWideCircularReferences;
		private Product[] _warmUpDeserialisedProductsFromBinaryFormatter, _warmUpDeserialisedProductsFromProtoBuf, _warmUpDeserialisedProductsFromDanSerialiser;

		[GlobalSetup]
		public void Setup()
		{
			// The json files (and the object model) are derived from real world data that I deal with (it's not the entire object model - because I got bored exporting it and tidyig it up - but it's enough
			// to work with). All of the text and location values have been changed to maintain their length and spread of characters (ie. not all ASCII) but to ensure that it's all anonymous (it's public
			// data anyway but it does no harm to err on the safe side).
			_products = new DirectoryInfo("SampleData")
				.EnumerateFiles("*.json")
				.Select(file => JsonConvert.DeserializeObject<Product>(File.ReadAllText(file.FullName)))
				.ToArray();
			_jsonNetSerialisedData = JsonNetSerialise();
			RegisterTypesWithProtoBufThatShareAssemblyAndNamespaceWith(typeof(Product));
			_binaryFormatterSerialisedData = BinaryFormatterSerialise();
			_warmUpDeserialisedProductsFromBinaryFormatter = BinaryFormatterDeserialise();
			_protoBufSerialisedData = ProtoBufSerialise();
			_warmUpDeserialisedProductsFromProtoBuf = ProtoBufDeserialise();
			_danSerialiserSerialisedData = DanSerialiserSerialise();
			_danSerialiserSerialisedDataOptimisedForWideCircularReferences = DanSerialiserSerialise_OptimisedForWideCircularReferences();
			_warmUpDeserialisedProductsFromDanSerialiser = DanSerialiserDeserialise();
		}

		[Benchmark]
		public string JsonNetSerialise()
		{
			return JsonConvert.SerializeObject(_products);
		}

		[Benchmark]
		public Product[] JsonNetDeserialise()
		{
			return JsonConvert.DeserializeObject<Product[]>(_jsonNetSerialisedData);
		}

		[Benchmark]
		public byte[] BinaryFormatterSerialise()
		{
			using (var stream = new MemoryStream())
			{
				(new BinaryFormatter()).Serialize(stream, _products);
				return stream.ToArray();
			}
		}

		[Benchmark]
		public Product[] BinaryFormatterDeserialise()
		{
			using (var stream = new MemoryStream(_binaryFormatterSerialisedData))
			{
				return (Product[])(new BinaryFormatter()).Deserialize(stream);
			}
		}

		[Benchmark]
		public byte[] ProtoBufSerialise()
		{
			using (var stream = new MemoryStream())
			{
				Serializer.Serialize(stream, _products);
				return stream.ToArray();
			}
		}

		[Benchmark]
		public Product[] ProtoBufDeserialise()
		{
			using (var stream = new MemoryStream(_protoBufSerialisedData))
			{
				return Serializer.Deserialize<Product[]>(stream);
			}
		}

		[Benchmark]
		public byte[] DanSerialiserSerialise()
		{
			return BinarySerialisation.Serialise(_products, optimiseForWideCircularReference: false);
		}


		[Benchmark]
		public byte[] DanSerialiserSerialise_OptimisedForWideCircularReferences()
		{
			return BinarySerialisation.Serialise(_products, optimiseForWideCircularReference: true);
		}

		[Benchmark]
		public Product[] DanSerialiserDeserialise()
		{
			return BinarySerialisation.Deserialise<Product[]>(_danSerialiserSerialisedData);
		}

		[Benchmark]
		public Product[] DanSerialiserDeserialise_OptimisedForWideCircularReferences()
		{
			return BinarySerialisation.Deserialise<Product[]>(_danSerialiserSerialisedDataOptimisedForWideCircularReferences);
		}

		private static void RegisterTypesWithProtoBufThatShareAssemblyAndNamespaceWith(Type sourceType)
		{
			foreach (var type in sourceType.Assembly.GetTypes().Where(t => t.Namespace == sourceType.Namespace))
			{
				string[] memberNames;
				if (type.IsEnum)
					memberNames = Enum.GetNames(type);
				else
					memberNames = type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.GetIndexParameters().Any()).Select(p => p.Name).ToArray();

				RuntimeTypeModel.Default.Add(type, false).Add(memberNames);
			}
		}
	}
}