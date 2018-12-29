using System;
using System.Runtime.Serialization;

namespace DanSerialiser
{
	[Serializable]
	public sealed class FastestTreeSerialisationNotPossibleException : Exception
	{
		private const string TYPE_NAME = "TypeName";
		private const string MEMBER_NAME = "MemberName";
		public FastestTreeSerialisationNotPossibleException(string typeName, ImpossibleMemberDetails memberIfAny) : base(GetMessage(typeName, memberIfAny))
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			TypeName = typeName;
			MemberIfAny = memberIfAny;
		}

		private static string GetMessage(string typeName, ImpossibleMemberDetails memberIfAny)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

			if (memberIfAny == null)
				return $"The type {typeName} does not allow optimal fastest tree binary serialisation - since no specific member is identified as a problem, it is presumably because the type is a non-sealed class";

			return $"The type {typeName} does not allow optimal fastest tree binary serialisation because the member {memberIfAny.Name} of type {memberIfAny.TypeName} does not allow it (presumably because the type is a non-sealed class)";
		}

		private FastestTreeSerialisationNotPossibleException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			TypeName = info.GetString(TYPE_NAME);	
			MemberIfAny = (ImpossibleMemberDetails)info.GetValue(MEMBER_NAME, typeof(ImpossibleMemberDetails));
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));

			info.AddValue(TYPE_NAME, TypeName);
			info.AddValue(MEMBER_NAME, MemberIfAny);
			base.GetObjectData(info, context);
		}

		public string TypeName { get; }
		public ImpossibleMemberDetails MemberIfAny { get; }

		[Serializable]
		public sealed class ImpossibleMemberDetails
		{
			public ImpossibleMemberDetails(string name, string typeName)
			{
				if (string.IsNullOrWhiteSpace(name))
					throw new ArgumentException($"Null/blank {nameof(name)} specified");
				if (string.IsNullOrWhiteSpace(typeName))
					throw new ArgumentException($"Null/blank {nameof(typeName)} specified");

				Name = name;
				TypeName = typeName;
			}

			public string Name { get; }
			public string TypeName { get; }
		}
	}
}