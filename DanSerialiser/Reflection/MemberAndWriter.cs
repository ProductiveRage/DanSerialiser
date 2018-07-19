using System;
using System.Reflection;

namespace DanSerialiser.Reflection
{
	internal sealed class MemberAndWriter<T> where T : MemberInfo
	{
		public MemberAndWriter(T member, MemberUpdater writerUnlessFieldShouldBeIgnored)
		{
			Member = member ?? throw new ArgumentNullException(nameof(member));
			WriterUnlessFieldShouldBeIgnored = writerUnlessFieldShouldBeIgnored;
		}

		public T Member { get; }
		public MemberUpdater WriterUnlessFieldShouldBeIgnored { get; }
	}
}