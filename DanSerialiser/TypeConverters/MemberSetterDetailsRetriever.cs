using System;

namespace DanSerialiser
{
	/// <summary>
	/// This will return null if it was not possible generate a member setter for the specified type (if the type is a non-sealed class, for example, then the fields and
	/// properties can not be known at analysis time because a type derived from it may add more fields or properties)
	/// </summary>
	public delegate MemberSetterDetails MemberSetterDetailsRetriever(Type type);
}