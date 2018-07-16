using System;

namespace DanSerialiser.Reflection
{
	internal sealed class PropertySetter
	{
		public PropertySetter(Type propertyType, Action<object, object> setter)
		{
			PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
			Setter = setter ?? throw new ArgumentNullException(nameof(setter));
		}

		public Type PropertyType { get; }
		public Action<object, object> Setter { get; }
	}
}