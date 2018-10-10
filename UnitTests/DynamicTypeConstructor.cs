using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace UnitTests
{
	internal static class DynamicTypeConstructor
	{
		public static ModuleBuilder GetModuleBuilder(string assemblyName, Version assemblyVersion)
		{
			if (string.IsNullOrWhiteSpace(assemblyName))
				throw new ArgumentException($"Null/blank {nameof(assemblyName)} specified");
			if (assemblyVersion == null)
				throw new ArgumentNullException(nameof(assemblyVersion));

			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
				new AssemblyName { Name = assemblyName, Version = assemblyVersion },
				AssemblyBuilderAccess.Run
			);
			return assemblyBuilder.DefineDynamicModule(assemblyBuilder.GetName().Name);
		}

		public static Type ConstructType(ModuleBuilder module, string typeNameWithNamespace, IEnumerable<Tuple<string, Type>> fields, Action<TypeBuilder> optionalFinisher = null)
		{
			if (module == null)
				throw new ArgumentNullException(nameof(module));
			if (string.IsNullOrWhiteSpace(typeNameWithNamespace))
				throw new ArgumentException($"Null/blank {nameof(typeNameWithNamespace)} specified");
			if (fields == null)
				throw new ArgumentNullException(nameof(fields));

			var typeBuilder = module.DefineType(typeNameWithNamespace);
			foreach (var field in fields)
				typeBuilder.DefineField(field.Item1, field.Item2, FieldAttributes.Public);
			optionalFinisher?.Invoke(typeBuilder);
			return typeBuilder.CreateType();
		}

		public static T ResolveDynamicAssembliesWhilePerformingAction<T>(Func<T> work, params Type[] dynamicTypes)
		{
			if (work == null)
				throw new ArgumentNullException(nameof(work));
			if (dynamicTypes == null)
				throw new ArgumentNullException(nameof(dynamicTypes));

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