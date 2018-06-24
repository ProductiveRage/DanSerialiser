using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class MemberAndReader<T> where T : MemberInfo
	{
		public MemberAndReader(T member, Func<object, object> reader)
		{
			Member = member;
			Reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		public T Member { get; }
		public Func<object, object> Reader { get; }
	}
}