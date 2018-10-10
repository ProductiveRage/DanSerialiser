using System;
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

			var list = PersistentList<T>.Empty;
			foreach (var item in items.Reverse())
				list = list.Insert(item);
			return list;
		}
	}

	internal sealed class PersistentList<T>
	{
		public static PersistentList<T> Empty { get; } = new PersistentList<T>(null);

		private readonly Node _headIfAny;
		private PersistentList(Node headIfAny)
		{
			_headIfAny = headIfAny;
		}

		public PersistentList<T> Insert(T value)
		{
			return new PersistentList<T>(new Node(value, _headIfAny));
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