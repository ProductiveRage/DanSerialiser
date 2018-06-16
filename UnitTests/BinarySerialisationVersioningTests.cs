using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DanSerialiser;
using Xunit;

namespace UnitTests
{
	public static class BinarySerialisationVersioningTests
	{
		[Fact]
		public static void EnsureThatDynamicallyCreatedTypesWork()
		{
			const string idFieldName = "Id";
			var type = ConstructType("DynamicAssemblyFor" + GetMyName(), new Version(1, 0), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });

			var instance = Activator.CreateInstance(type);
			var field = type.GetField(idFieldName);
			field.SetValue(instance, 123);

			var clone = CloneAndSupportDynamicAssemblies(instance, type);
			Assert.Equal(123, field.GetValue(clone));
		}

		/// <summary>
		/// For when serialising and deserialising an object to / from different versions of an assembly, if the serialised instance was newer and has more properties (and these
		/// properties don't exist on the deserialisation type) then ignore them
		/// </summary>
		[Fact]
		public static void IgnoreFieldsInDataNotPresentOnDeserialisationType()
		{
			const string idFieldName = "Id";
			const string nameFieldName = "Name";
			var sourceType = ConstructType(
				"DynamicAssemblyFor" + GetMyName(),
				new Version(1, 0),
				"ClassWithIntId",
				new[]
				{
					Tuple.Create(idFieldName, typeof(int)),
					Tuple.Create(nameFieldName, typeof(string))
				}
			);

			var instance = Activator.CreateInstance(sourceType);
			var idFieldOnSource = sourceType.GetField(idFieldName);
			idFieldOnSource.SetValue(instance, 123);
			var serialisedData = BinarySerialisationCloner.Serialise(instance);

			var destinationType = ConstructType("DynamicAssemblyFor" + GetMyName(), new Version(1, 0), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var idFieldOnDestination = destinationType.GetField(idFieldName);
			Assert.Equal(123, idFieldOnDestination.GetValue(clone));
		}

		/// <summary>
		/// If serialising an object to / from different versions of an assembly, if the destination type has a field that the serialised data does not have any information for
		/// then skip it (this is intended to give some flexibility with versioning - if fields are added or remove or renamed - but it could lead to deserialised instances having
		/// uninitialised values in unexpected places and so this approach may change soon)
		/// </summary>
		[Fact]
		public static void DoNotWorryIfSerialisedDataCanNotSetAllFields()
		{
			const string idFieldName = "Id";
			var sourceType = ConstructType("DynamicAssemblyFor" + GetMyName(), new Version(1, 0), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });

			var instance = Activator.CreateInstance(sourceType);
			var idFieldOnSource = sourceType.GetField(idFieldName);
			idFieldOnSource.SetValue(instance, 123);
			var serialisedData = BinarySerialisationCloner.Serialise(instance);

			const string nameFieldName = "Name";
			var destinationType = ConstructType(
				"DynamicAssemblyFor" + GetMyName(),
				new Version(1, 0),
				"ClassWithIntId",
				new[]
				{
					Tuple.Create(idFieldName, typeof(int)),
					Tuple.Create(nameFieldName, typeof(string))
				}
			);
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var idFieldOnDestination = destinationType.GetField(idFieldName);
			var nameFieldOnDestination = destinationType.GetField(nameFieldName);
			Assert.Equal(123, idFieldOnDestination.GetValue(clone));
			Assert.Equal((string)null, nameFieldOnDestination.GetValue(clone));
		}

		/// <summary>
		/// Support properties to be replaced on future versions of the type and for the serialised data generated from that future version to be deserialised assemblies that
		/// have older versions of the type - this will only work if the replaced properties continue to exist on the future version but as computed properties that return (and
		/// set, if the type should be mutable) values derived from the new data AND if the computed properties have the [Deprecated] attribute on them.
		/// </summary>
		[Fact]
		public static void DeprecatedPropertyShouldBeSerialisedAsIfItIsAnAutoProperty()
		{
			// In a real world scenario, the "future type" would have other properties and the [Deprecated] one(s) would have values computed from them.. for the interests of this
			// unit test, the future type (typeWithDeprecatedProperty) will ONLY have a [Deprecated] property that has a "computed value" (ie. a getter that always returns 123)
			const string idPropertyName = "Id";
			const int hardCodedIdValueForDeprecatedProperty = 123;
			var typeWithDeprecatedProperty = ConstructType(
				"DynamicAssemblyFor" + GetMyName(),
				new Version(1, 0),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					var propertyBuilder = typeBuilder.DefineProperty(idPropertyName, PropertyAttributes.None, typeof(int), parameterTypes: Type.EmptyTypes);
					propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(DeprecatedAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
					var getterBuilder = typeBuilder.DefineMethod("get_" + idPropertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), parameterTypes: Type.EmptyTypes);
					var ilGenerator = getterBuilder.GetILGenerator();
					ilGenerator.Emit(OpCodes.Ldc_I4, hardCodedIdValueForDeprecatedProperty);
					ilGenerator.Emit(OpCodes.Ret);
					propertyBuilder.SetGetMethod(getterBuilder);
				}
			);
			var instance = Activator.CreateInstance(typeWithDeprecatedProperty);
			var serialisedData = BinarySerialisationCloner.Serialise(instance);

			// For the sake of this test, the type that will be deserialised to only needs to have a backing field for an auto property (the field that we're expecting to
			// get set) and so that's all that is being configured. It would be closer to a real use case if there was a property with a getter that used this backing field
			// but it wouldn't be used by this test and so it doesn't need to be created.
			var idBackingFieldName = BackingFieldHelpers.GetBackingFieldName(idPropertyName);
			var typeThatStillHasThePropertyOnIt = ConstructType(
				"DynamicAssemblyFor" + GetMyName(),
				new Version(1, 0),
				"MyClass",
				new[] { Tuple.Create(idBackingFieldName, typeof(int)) }
			);
			var deserialised = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, typeThatStillHasThePropertyOnIt),
				typeThatStillHasThePropertyOnIt
			);
			var idBackingFieldOnDestination = typeThatStillHasThePropertyOnIt.GetField(idBackingFieldName);
			Assert.Equal(123, idBackingFieldOnDestination.GetValue(deserialised));
		}

		/// <summary>
		/// Use the CallerMemberName attribute so that a method can gets its own name so that it can specify a descriptive dynamic assembly name (could have used nameof
		/// but that would invite copy-paste errors when new methods were added - the method name need to be changed AND the reference to it within the nameof call)
		/// </summary>
		private static string GetMyName([CallerMemberName] string callerName = null)
		{
			return callerName;
		}

		private static Type ConstructType(string assemblyName, Version assemblyVersion, string typeNameWithNamespace, IEnumerable<Tuple<string, Type>> fields, Action<TypeBuilder> optionalFinisher = null)
		{
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
				new AssemblyName { Name = assemblyName, Version = assemblyVersion },
				AssemblyBuilderAccess.Run
			);
			var module = assemblyBuilder.DefineDynamicModule(assemblyBuilder.GetName().Name);
			var typeBuilder = module.DefineType(typeNameWithNamespace);
			foreach (var field in fields)
				typeBuilder.DefineField(field.Item1, field.Item2, FieldAttributes.Public);
			optionalFinisher?.Invoke(typeBuilder);
			return typeBuilder.CreateType();
		}

		/// <summary>
		/// This calls the BinarySerialisationCloner Deserialise method but populates the generic type parameter with a runtime type value
		/// </summary>
		private static object Deserialise(byte[] serialisedData, Type type)
		{
			return typeof(BinarySerialisationCloner).GetMethod(nameof(Deserialise), new[] { typeof(byte[]) }).MakeGenericMethod(type).Invoke(null, new[] { serialisedData });
		}

		private static T CloneAndSupportDynamicAssemblies<T>(T value, params Type[] dynamicTypes)
		{
			return ResolveDynamicAssembliesWhilePerformingAction<T>(
				() => BinarySerialisationCloner.Clone(value),
				dynamicTypes
			);
		}

		private static T ResolveDynamicAssembliesWhilePerformingAction<T>(Func<T> work, params Type[] dynamicTypes)
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			try
			{
				return work();
			}
			finally
			{
				AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;
			}

			Assembly AssemblyResolve(object sender, ResolveEventArgs args)
			{
				if (dynamicTypes != null)
				{
					foreach (var type in dynamicTypes)
					{
						if (args.Name == type.Assembly.FullName)
							return type.Assembly;
					}
				}
				return null;
			}
		}
	}
}