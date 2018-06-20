using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	/// <summary>
	/// These tests exist because I realised that I couldn't remember precisely why I was adding the constructor that takes (SerializationInfo info, StreamingContext context) and whether
	/// or not I need to override GetObjectData if there are additional properties and what serialisation mechanisms are and aren't supported
	/// </summary>
	public static class ExceptionSerialisationTests
	{
		[Fact]
		public static void CircularReferenceExceptionCanBeSerialisedWithBinaryFormatter()
		{
			Assert.IsType<CircularReferenceException>(CloneWithBinaryFormatter(new CircularReferenceException()));
		}

		[Fact]
		public static void CircularReferenceExceptionCanBeSerialisedWithDanSerialiser()
		{
			Assert.IsType<CircularReferenceException>(BinarySerialisationCloner.Clone(new CircularReferenceException(), supportReferenceReuse: false));
		}

		[Fact]
		public static void FieldNotPresentInSerialisedDataExceptionCanBeSerialisedWithBinaryFormatter()
		{
			var clone = CloneWithBinaryFormatter(new FieldNotPresentInSerialisedDataException("MyType", "MyField"));
			Assert.IsType<FieldNotPresentInSerialisedDataException>(clone);
			Assert.Equal("MyType", clone.TypeName);
			Assert.Equal("MyField", clone.FieldName);
		}

		[Fact]
		public static void FieldNotPresentInSerialisedDataExceptionCanBeSerialisedWithDanSerialiser()
		{
			var clone = BinarySerialisationCloner.Clone(new FieldNotPresentInSerialisedDataException("MyType", "MyField"), supportReferenceReuse: false);
			Assert.IsType<FieldNotPresentInSerialisedDataException>(clone);
			Assert.Equal("MyType", clone.TypeName);
			Assert.Equal("MyField", clone.FieldName);
		}

		/// <summary>
		/// XML Serialisation doesn't like Exceptions - it doesn't work with the Base Exception and so it's not my fault that it's not working with my custom exceptions
		/// </summary>
		[Fact]
		public static void ExceptionsCanNotBeSerialisedWithXmlSerialiser()
		{
			Assert.Throws<InvalidOperationException>(() => CloneWithXmlSerializer(new Exception("Example Exception")));
		}

		private static T CloneWithBinaryFormatter<T>(T value)
		{
			var formatter = new BinaryFormatter();
			byte[] serialisedException;
			using (var stream = new MemoryStream())
			{
				formatter.Serialize(stream, value);
				serialisedException = stream.ToArray();
			}
			using (var stream = new MemoryStream(serialisedException))
			{
				return (T)formatter.Deserialize(stream);
			}
		}

		private static T CloneWithXmlSerializer<T>(T value)
		{
			var serialiser = new XmlSerializer(typeof(T));
			string serialisedException;
			using (var writer = new StringWriter())
			{
				serialiser.Serialize(writer, value);
				serialisedException = writer.ToString();
			}
			using (var reader = new StringReader(serialisedException))
			{
				return (T)serialiser.Deserialize(reader);
			}
		}
	}
}