using System;
using System.Drawing;
using System.Linq;
using DanSerialiser;
using DanSerialiser.TypeConverters;
using Xunit;

namespace UnitTests
{
	public static class BinarySerialisationTypeConverterTests
	{
		/// <summary>
		/// This is a simple example to demonstrate how type converters work (and is not necessarily intended to ilustrate where they SHOULD be used)
		/// </summary>
		[Fact]
		public static void PointMayBeSerialisedAsArray()
		{
			var value = new Point { X = 10, Y = 20 };
			var serialisationTypeConverter = new PointToArrayTypeConverter();
			var deserialisationTypeConverter = new ArrayToPointTypeConverter();
			var clone = BinarySerialisationCloner.Clone(value, new[] { serialisationTypeConverter }, new[] { deserialisationTypeConverter }, ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure);
			Assert.Equal(value, clone);
			Assert.Equal(1, serialisationTypeConverter.NumberOfValuesChanged); // Should have changed one Point into an array
			Assert.Equal(2, serialisationTypeConverter.NumberOfValuesNotChanged); // Should have encountered X and Y values that were not changed for serialisation
			Assert.Equal(1, deserialisationTypeConverter.NumberOfValuesChanged); // Should have changed one array into a Point
			Assert.Equal(2, deserialisationTypeConverter.NumberOfValuesNotChanged); // Should have encountered X and Y values that were not changed for deserialisation
		}

		[Fact]
		public static void PointPropertyMayBeSerialisedAsArray()
		{
			var value = new { Location = new  Point { X = 10, Y = 20 } };
			var serialisationTypeConverter = new PointToArrayTypeConverter();
			var deserialisationTypeConverter = new ArrayToPointTypeConverter();
			var clone = BinarySerialisationCloner.Clone(value, new[] { serialisationTypeConverter }, new[] { deserialisationTypeConverter }, ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure);
			Assert.Equal(value, clone);
			Assert.Equal(1, serialisationTypeConverter.NumberOfValuesChanged); // Should have changed one Point into an array
			Assert.Equal(3, serialisationTypeConverter.NumberOfValuesNotChanged); // Should have encountered one instance of an anonymous type and X and Y values that weren't changed for serialisation
			Assert.Equal(1, deserialisationTypeConverter.NumberOfValuesChanged); // Should have changed one array into a Point
			Assert.Equal(3, deserialisationTypeConverter.NumberOfValuesNotChanged); // Should have encountered one instance of an anonymous type and X and Y values that weren't changed for deserialisation
		}

		/// <summary>
		/// When serialising a linked list, the Serialiser will start at the first node and then follow the property linking to the next node and then the next node, meaning that each node
		/// adds a layer to the call stack - if the list is large then this can result in a stack overflow exception. A workaround for this is to have a type converter that changes the linked
		/// list into an array just before it gets serialised and then changes the array back into the linked list just after it gets deserialised (so long as the target type IS a linked list
		/// type - we don't want to change ALL arrays into linked lists after they are deserialised). Note: It's difficult to create a unit test that illustrates the serialisation would fail
		/// with a larged linked list WITHOUT a type converter like this because xunit / the VS test runner doesn't seem to handle stack overflow exceptions very well!
		/// </summary>
		[Fact]
		public static void AvoidLinkedListStackOverflow()
		{
			var value = PersistentList.Of(Enumerable.Range(0, 3000));
			var typeConverters = new[] { PersistentListTypeConverter.Instance };
			var clone = BinarySerialisationCloner.Clone(value, typeConverters, typeConverters, ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure);
			Assert.Equal(value.ToArray(), clone.ToArray());
		}

		/* Note: It's difficult to create a unit test that illustrates that serialisation would fail without a type converter as shown above because xUnit (or the VS Test Runner, I'm not
		   sure which is "at fault") doesn't allows tests to be written that expect a StackOverflowException to be thrown - it will result in the test runner logging that "The active test
		   run was aborted. Reason: Process is terminating due to StackOverflowException." instead of it realising that a stack overflow was the expected result
		[Fact]
		public static void ThisTestWouldIllustrateStackOverflowExceptionExceptThatItCanNotBeRun()
		{
			var value = PersistentList.Of(Enumerable.Range(0, 3000));
			Assert.Throws<StackOverflowException>(() => BinarySerialisation.Serialise(value));
		}
		*/

		public sealed class PointToArrayTypeConverter : ISerialisationTypeConverter
		{
			public int NumberOfValuesChanged = 0; // This is here for the unit tests
			public int NumberOfValuesNotChanged = 0; // This is here for the unit tests

			public object ConvertIfRequired(object value)
			{
				if (value is Point point)
				{
					NumberOfValuesChanged++;
					return new[] { point.X, point.Y };
				}
				NumberOfValuesNotChanged++;
				return value;
			}
		}

		public sealed class ArrayToPointTypeConverter : IDeserialisationTypeConverter
		{
			public int NumberOfValuesChanged = 0; // This is here for the unit tests
			public int NumberOfValuesNotChanged = 0; // This is here for the unit tests

			public object ConvertIfRequired(Type targetType, object value)
			{
				if ((value is int[] array) && (array.Length == 2) && (targetType == typeof(Point)))
				{
					NumberOfValuesChanged++;
					return new Point { X = array[0], Y = array[1] };
				}
				NumberOfValuesNotChanged++;
				return value;
			}
		}
	}
}