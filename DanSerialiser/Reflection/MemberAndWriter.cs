using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class MemberAndWriter<T> where T : MemberInfo
	{
		public MemberAndWriter(T member, Action<object, object> writerUnlessFieldShouldBeIgnored)
		{
			Member = member ?? throw new ArgumentNullException(nameof(member));
			WriterUnlessFieldShouldBeIgnored = writerUnlessFieldShouldBeIgnored;
		}

		public T Member { get; }
		public Action<object, object> WriterUnlessFieldShouldBeIgnored { get; }
	}
}