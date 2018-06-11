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

		private static T CloneAndSupportDynamicAssemblies<T>(T value, params Type[] dynamicTypes)
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			try
			{
				return BinarySerialisationCloner.Clone(value);
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