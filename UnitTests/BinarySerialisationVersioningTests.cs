using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
		/// Use the CallerMemberName attribute so that a method can gets its own name so that it can specify a descriptive dynamic assembly name (could have used nameof
		/// but that would invite copy-paste errors when new methods were added - the method name need to be changed AND the reference to it within the nameof call)
		/// </summary>
		private static string GetMyName([CallerMemberName] string callerName = null)
		{
			return callerName;
		}

		private static Type ConstructType(string assemblyName, Version assemblyVersion, string typeNameWithNamespace, IEnumerable<Tuple<string, Type>> fields)
		{
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
				new AssemblyName { Name = assemblyName, Version = assemblyVersion },
				AssemblyBuilderAccess.Run
			);
			var module = assemblyBuilder.DefineDynamicModule(assemblyBuilder.GetName().Name);
			var typeBuilder = module.DefineType(typeNameWithNamespace);
			foreach (var field in fields)
				typeBuilder.DefineField(field.Item1, field.Item2, FieldAttributes.Public);
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
				AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
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