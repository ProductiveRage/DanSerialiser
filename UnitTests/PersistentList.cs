using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnitTests
{
	internal static class PersistentList
	{
		public static PersistentList<T> Of<T>(IEnumerable<T> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			return PersistentList<T>.Empty.InsertRange(items);
		}
	}

	internal sealed class PersistentList<T> : IEnumerable<T>
	{
		public static PersistentList<T> Empty { get; } = new PersistentList<T>(null);

		private readonly Node _headIfAny;
		private PersistentList(Node headIfAny)
		{
			_headIfAny = headIfAny;
		}

		public PersistentList<T> Insert(T value) => new PersistentList<T>(new Node(value, _headIfAny));

		public PersistentList<T> InsertRange(IEnumerable<T> values)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));

			var node = _headIfAny;
			foreach (var value in values.Reverse())
				node = new Node(value, node);
			return new PersistentList<T>(node);
		}

		public T[] ToArray()
		{
			var items = new List<T>();
			var node = _headIfAny;
			while (node != null)
			{
				items.Add(node.Value);
				node = node.NextIfAny;
			}
			return items.ToArray();
		}

		public IEnumerator<T> GetEnumerator()
		{
			var node = _headIfAny;
			while (node != null)
			{
				yield return node.Value;
				node = node.NextIfAny;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private sealed class Node
		{
			public Node(T value, Node nextIfAny)
			{
				Value = value;
				NextIfAny = nextIfAny;
			}
			public T Value { get; }
			public Node NextIfAny { get; }
		}
	}
}