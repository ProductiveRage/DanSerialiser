using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DanSerialiser;
using DanSerialiser.Reflection;
using Xunit;
using static DanSerialiser.CachedLookups.BinarySerialisationCompiledMemberSetters;
using static UnitTests.DynamicTypeConstructor;

namespace UnitTests
{
	public static class SimpleMemberSetterCompilationVersioning
	{
		[Fact]
		public static void DeprecatedPropertyIsWrittenAgainstOldPropertyNameAsWellAsNew()
		{
			// Define two versions of the same class where the V1 looks like this:
			//
			//	  public class SomethingWithName
			//    {
			//        public string NameOld { get; set; }
			//    }
			//
			// .. and the V2 looks like this:
			//
			//	  public class SomethingWithName
			//    {
			//        public string NameNew { get; set; }
			//
			//        [Deprecated]
			//        public string NameOld { get { return NameNew; } }
			//    }
			//
			// If an instance of V2 entity is serialised and then deserialised into the V1 entity type then the "NameOld" field of the V1 entity instance should be populated (in order for
			// this to work, the member-setter-generation logic needs to consider fields and properties and the attributes that they do or do not have)
			var entityTypeV1 = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"SomethingWithName",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Backing field for "NameOld"
					var nameOldfieldBuilder = typeBuilder.DefineField(BackingFieldHelpers.GetBackingFieldName("NameOld"), typeof(string), FieldAttributes.Private);

					// Property for "NameOld"
					var nameOldPropertyBuilder = typeBuilder.DefineProperty("NameOld", PropertyAttributes.None, typeof(string), parameterTypes: Type.EmptyTypes);
					var nameOldGetterBuilder = typeBuilder.DefineMethod("get_NameOld", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), parameterTypes: Type.EmptyTypes);
					var ilGeneratorForNameOldGetter = nameOldGetterBuilder.GetILGenerator();
					ilGeneratorForNameOldGetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForNameOldGetter.Emit(OpCodes.Ldfld, nameOldfieldBuilder);
					ilGeneratorForNameOldGetter.Emit(OpCodes.Ret);
					nameOldPropertyBuilder.SetGetMethod(nameOldGetterBuilder);
					var nameOldSetterBuilder = typeBuilder.DefineMethod("set_NameOld", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), parameterTypes: new[] { typeof(string) });
					var ilGeneratorForNameOldSetter = nameOldSetterBuilder.GetILGenerator();
					ilGeneratorForNameOldSetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForNameOldSetter.Emit(OpCodes.Ldarg_1);
					ilGeneratorForNameOldSetter.Emit(OpCodes.Stfld, nameOldfieldBuilder);
					ilGeneratorForNameOldSetter.Emit(OpCodes.Ret);
					nameOldPropertyBuilder.SetSetMethod(nameOldSetterBuilder);
				}
			);
			var entityTypeV2 = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"SomethingWithName",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Backing field for "NameNew"
					var nameNewfieldBuilder = typeBuilder.DefineField(BackingFieldHelpers.GetBackingFieldName("NameNew"), typeof(string), FieldAttributes.Private);

					// Property for "NameNew"
					var nameNewPropertyBuilder = typeBuilder.DefineProperty("NameNew", PropertyAttributes.None, typeof(string), parameterTypes: Type.EmptyTypes);
					var nameNewGetterBuilder = typeBuilder.DefineMethod("get_NameNew", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), parameterTypes: Type.EmptyTypes);
					var ilGeneratorForNameNewGetter = nameNewGetterBuilder.GetILGenerator();
					ilGeneratorForNameNewGetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForNameNewGetter.Emit(OpCodes.Ldfld, nameNewfieldBuilder);
					ilGeneratorForNameNewGetter.Emit(OpCodes.Ret);
					nameNewPropertyBuilder.SetGetMethod(nameNewGetterBuilder);
					var nameNewSetterBuilder = typeBuilder.DefineMethod("set_NameNew", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), parameterTypes: new[] { typeof(string) });
					var ilGeneratorForNameNewSetter = nameNewSetterBuilder.GetILGenerator();
					ilGeneratorForNameNewSetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForNameNewSetter.Emit(OpCodes.Ldarg_1);
					ilGeneratorForNameNewSetter.Emit(OpCodes.Stfld, nameNewfieldBuilder);
					ilGeneratorForNameNewSetter.Emit(OpCodes.Ret);
					nameNewPropertyBuilder.SetSetMethod(nameNewSetterBuilder);

					// Property for "NameOld" that has [Deprecated] attribute and whose getter access "NameNew"
					var nameOldPropertyBuilder = typeBuilder.DefineProperty("NameOld", PropertyAttributes.None, typeof(string), parameterTypes: Type.EmptyTypes);
					var nameOldGetterBuilder = typeBuilder.DefineMethod("get_NameOld", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), parameterTypes: Type.EmptyTypes);
					var ilGeneratorForNameOldGetter = nameOldGetterBuilder.GetILGenerator();
					ilGeneratorForNameOldGetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForNameOldGetter.Emit(OpCodes.Call, nameNewGetterBuilder);
					ilGeneratorForNameOldGetter.Emit(OpCodes.Ret);
					nameOldPropertyBuilder.SetGetMethod(nameOldGetterBuilder);
					nameOldPropertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(DeprecatedAttribute).GetConstructor(new[] { typeof(string) }), new object[] { null }));
				}
			);

			// Create an instance of the V2 entity
			var source = Activator.CreateInstance(entityTypeV2);
			entityTypeV2.GetProperty("NameNew").SetValue(source, "abc");

			// Try to create a member setter for it - this should work since it only has string fields and properties
			var memberSetterDetails =
				GetMemberSetterAvailability(
					entityTypeV2,
					DefaultTypeAnalyser.Instance,
					valueWriterRetriever: t => null // No complex nested member setter madness required, so provide a valueWriterRetriever delegate that always returns null
				)
				.MemberSetterDetailsIfSuccessful;
			Assert.NotNull(memberSetterDetails);

			// Serialise this v2 entity instance
			byte[] serialised;
			using (var stream = new MemoryStream())
			{
				foreach (var fieldName in memberSetterDetails.FieldsSet)
				{
					var fieldNameBytes = new[] { (byte)BinarySerialisationDataType.FieldNamePreLoad }.Concat(fieldName.AsStringAndReferenceID).ToArray();
					stream.Write(fieldNameBytes, 0, fieldNameBytes.Length);
				}
				var writer = new BinarySerialisationWriter(stream);
				writer.ObjectStart(source.GetType());
				memberSetterDetails.GetCompiledMemberSetter()(source, writer);
				writer.ObjectEnd();
				serialised = stream.ToArray();
			}

			// Ensure that it works deserialising back to an older version of the type
			var deserialisedAsV1 = ResolveDynamicAssembliesWhilePerformingAction(
				() => BinarySerialisation.Deserialise<object>(serialised),
				entityTypeV1
			);
			Assert.NotNull(deserialisedAsV1);
			Assert.IsType(entityTypeV1, deserialisedAsV1);
			Assert.Equal("abc", deserialisedAsV1.GetType().GetProperty("NameOld").GetValue(deserialisedAsV1));
		}

		private static string GetMyName([CallerMemberName] string callerName = null) => callerName;
	}
}