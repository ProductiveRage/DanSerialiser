﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DanSerialiser;
using Xunit;
using static UnitTests.DynamicTypeConstructor;

namespace UnitTests
{
	public static class BinarySerialisationVersioningTests
	{
		[Fact]
		public static void EnsureThatDynamicallyCreatedTypesWork()
		{
			const string idFieldName = "Id";
			var type = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });

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
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
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
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var idFieldOnDestination = destinationType.GetField(idFieldName);
			Assert.Equal(123, idFieldOnDestination.GetValue(clone));
		}

		/// <summary>
		/// This is an extension to IgnoreFieldsInDataNotPresentOnDeserialisationType above that tests a fix that ensures that we don't call any deserialisation type converters for
		/// fields that do not exist in the target type because this may result in an exception (the deserialisation type converter would not know what the destination type should
		/// be because the destination field does not exist, without this information it is not possible for the type converter to do anything, which is why the summary comment on
		/// the ConvertIfRequired states that it will not be called with a null 'targetType' reference)
		/// </summary>
		[Fact]
		public static void IgnoreFieldsInDataNotPresentOnDeserialisationTypeWhenDeserialisationTypeConvertersUsed()
		{
			const string idFieldName = "Id";
			const string nameFieldName = "Name";
			var sourceType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
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
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "ClassWithIntId", new[] { Tuple.Create(idFieldName, typeof(int)) });
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType, new[] { NullDeserialisationTypeConverter.Instance }),
				destinationType
			);
			var idFieldOnDestination = destinationType.GetField(idFieldName);
			Assert.Equal(123, idFieldOnDestination.GetValue(clone));
		}

		private sealed class NullDeserialisationTypeConverter : IDeserialisationTypeConverter
		{
			public static NullDeserialisationTypeConverter Instance { get; } = new NullDeserialisationTypeConverter();
			private NullDeserialisationTypeConverter() { }
			public object ConvertIfRequired(Type targetType, object value)
			{
				if (targetType == null)
					throw new ArgumentNullException(nameof(targetType));
				return value;
			}
		}

		/// <summary>
		/// If deserialising data where an older version of the type was serialised and the new version that is being deserialised to has a field that the old one did not have then
		/// throw an exception. For a type to be deserialised and for some of its fields not to be set could result in confusing errors in some cases - for example, if the type has
		/// a constructor that sets all fields to non-null values then consumers of that class could reasonably expect to never have to deal with a null value on any of that type's
		/// properties but if the deserialisation process is allowed to leave some of those fields null then those expectations will not be met and null reference exceptions could
		/// </summary>
		[Fact]
		public static void IfFieldCanNotBeSetDuringDeserialisationThenThrow()
		{
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var instance = Activator.CreateInstance(sourceType);
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				new[] { Tuple.Create("Id", typeof(int)) }
			);
			Assert.Throws<FieldNotPresentInSerialisedDataException>(() =>
				ResolveDynamicAssembliesWhilePerformingAction(
					() => Deserialise(serialisedData, destinationType),
					destinationType
				)
			);
		}

		/// <summary>
		/// This relates to IfFieldCanNotBeSetDuringDeserialisationThenThrow - ordinarily, if the serialisation data can't set a value on a field then that should mean that the
		/// deserialisation has failed but if that field has the [OptionalWhenDeserialising] on it then that means that it's ok
		/// </summary>
		[Fact]
		public static void AllowDeserialisationIfFieldCanNotBeSetIfFieldIsMarkedAsOptionalForDeserialisation()
		{
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var instance = Activator.CreateInstance(sourceType);
			var serialisedData = BinarySerialisation.Serialise(instance);

			const string nameFieldName = "Name";
			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Need to define the field using this lambda rather than specifying it through the fields argument because we need to set the custom attribute on it
					var fieldBuilder = typeBuilder.DefineField(nameFieldName, typeof(string), FieldAttributes.Public);
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(OptionalWhenDeserialisingAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
				}
			);
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var nameFieldOnDestination = destinationType.GetField(nameFieldName);
			Assert.Null(nameFieldOnDestination.GetValue(clone));
		}

		/// <summary>
		/// This is the same principle as AllDeserialisationIfFieldCanNotBeSetIfFieldIsMarkedAsOptionalForDeserialisation but for the case where there is an auto-property that has
		/// the [OptionalWhenDeserialising] attribute on it
		/// </summary>
		[Fact]
		public static void AllowDeserialisationIfFieldCanNotBeSetIfFieldIsForAutoPropertyThatIsMarkedAsOptionalForDeserialisation()
		{
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var instance = Activator.CreateInstance(sourceType);
			var serialisedData = BinarySerialisation.Serialise(instance);

			const string namePropertyName = "Name";
			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Need to define the field using this lambda rather than specifying it through the fields argument because we need the reference for use in the property getter
					var fieldBuilder = typeBuilder.DefineField(BackingFieldHelpers.GetBackingFieldName(namePropertyName), typeof(string), FieldAttributes.Private);
					var propertyBuilder = typeBuilder.DefineProperty(namePropertyName, PropertyAttributes.None, typeof(string), parameterTypes: Type.EmptyTypes);
					propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(DeprecatedAttribute).GetConstructor(new[] { typeof(string) }), new object[] { null }));
					var getterBuilder = typeBuilder.DefineMethod("get_" + namePropertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), parameterTypes: Type.EmptyTypes);
					var ilGenerator = getterBuilder.GetILGenerator();
					ilGenerator.Emit(OpCodes.Ldarg_0);
					ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
					ilGenerator.Emit(OpCodes.Ret);
					propertyBuilder.SetGetMethod(getterBuilder);
					propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(OptionalWhenDeserialisingAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
				}
			);
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var namePropertyOnDestination = destinationType.GetProperty(namePropertyName);
			Assert.Null(namePropertyOnDestination.GetValue(clone));
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
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					var propertyBuilder = typeBuilder.DefineProperty(idPropertyName, PropertyAttributes.None, typeof(int), parameterTypes: Type.EmptyTypes);
					propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(DeprecatedAttribute).GetConstructor(new[] { typeof(string) }), new object[] { null }));
					var getterBuilder = typeBuilder.DefineMethod("get_" + idPropertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), parameterTypes: Type.EmptyTypes);
					var ilGenerator = getterBuilder.GetILGenerator();
					ilGenerator.Emit(OpCodes.Ldc_I4, hardCodedIdValueForDeprecatedProperty);
					ilGenerator.Emit(OpCodes.Ret);
					propertyBuilder.SetGetMethod(getterBuilder);
				}
			);
			var instance = Activator.CreateInstance(typeWithDeprecatedProperty);
			var serialisedData = BinarySerialisation.Serialise(instance);

			// For the sake of this test, the type that will be deserialised to only needs to have a backing field for an auto property (the field that we're expecting to
			// get set) and so that's all that is being configured. It would be closer to a real use case if there was a property with a getter that used this backing field
			// but it wouldn't be used by this test and so it doesn't need to be created.
			var idBackingFieldName = BackingFieldHelpers.GetBackingFieldName(idPropertyName);
			var typeThatStillHasThePropertyOnIt = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
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
		/// This also tests backward compatibility - if a version of a class C1 has a field 'Value' that is of type C2 and this is serialised and then deserialised somewhere
		/// with a version of C1 that does not have the 'Value' field and does not have the type C2 available then it shouldn't matter that C2 can't be loaded because the C2
		/// value would never be used to set anything
		/// </summary>
		[Fact]
		public static void DoNotThrowWhenTryingToParseUnavailableTypeIfThereIsNoFieldThatTheTypeWouldBeUsedToSet()
		{
			// Declare a type that has a field that is of another type that is declared here. The destination type will be in an assembly that does not have this second type
			// in it but the destination type also won't have the field and so the deserialiser should be able to skip over the data about the field that we don't care about.
			var valueFieldName = "Value";
			var sourceModule = GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0));
			var nestedSourceType = ConstructType(sourceModule, "MyNestedClass", new Tuple<string, Type>[0]);
			var sourceType = ConstructType(sourceModule, "MyClass", new[] { Tuple.Create(valueFieldName, nestedSourceType) });
			var instance = Activator.CreateInstance(sourceType);
			sourceType.GetField(valueFieldName).SetValue(instance, Activator.CreateInstance(nestedSourceType));
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var deserialised = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			Assert.IsType(destinationType, deserialised);
		}

		/// <summary>
		/// This also tests backward compatibility - if a version of a class C1 has field 'Items' that is *an array* of element type C2 and this is serialised
		/// and then deserialised somewhere with a version of C1 that does not have the 'Items' field and does not have the type C2 available then it shouldn't
		/// matter that C2 can't be loaded because the C2 value would never be used to set anything.
		/// This tests the change added in https://github.com/ProductiveRage/DanSerialiser/pull/4.
		/// </summary>
		[Fact]
		public static void DoNotThrowWhenTryingToParseUnavailableArrayElementTypeIfThereIsNoFieldThatTheTypeWouldBeUsedToSet()
		{
			// Declare a type that has a field that is an array of another element type that is declared here. The destination type will be in an assembly that
			// does not have this second type in it but the destination type also won't have the field and so the deserialiser should be able to skip over the
			// data about the field that we don't care about.
			var itemsFieldName = "Items";
			var sourceModule = GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0));
			var nestedSourceType = ConstructType(sourceModule, "MyNestedClass", new Tuple<string, Type>[0]);
			var nestedSourceTypeArrayType = nestedSourceType.MakeArrayType();
			var sourceType = ConstructType(sourceModule, "MyClass", new[] { Tuple.Create(itemsFieldName, nestedSourceTypeArrayType) });
			var sourceInstance = Activator.CreateInstance(sourceType);
			var itemsArray = Array.CreateInstance(nestedSourceType, 3);
			for (int i = 0; i < itemsArray.Length; i++)
				itemsArray.SetValue(Activator.CreateInstance(nestedSourceType), i);
			sourceType.GetField(itemsFieldName).SetValue(sourceInstance, itemsArray);
			var serialisedData = BinarySerialisation.Serialise(sourceInstance);

			var destinationType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var deserialised = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			Assert.IsType(destinationType, deserialised);
		}

		/// <summary>
		/// This is a companion to DoNotThrowWhenTryingToParseUnavailableTypeIfThereIsNoFieldThatTheTypeWouldBeUsedToSet to illustrate that it is NOT ok to ignore a type
		/// that is not available if the instance of that type would be used to populate a property
		/// </summary>
		[Fact]
		public static void DeserialisationWillFailIfTypeRequiredToSetFieldValueIsNotAvailable()
		{
			// The source type will have a name that matches the deserialisation type but the "Value" property will have different types on the source and destination types
			// and so the deserialisation attempt should fail
			var valueFieldName = "Value";
			var sourceModule = GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0));
			var nestedSourceType = ConstructType(sourceModule, "MyNestedClass", new Tuple<string, Type>[0]);
			var sourceType = ConstructType(sourceModule, "MyClass", new[] { Tuple.Create(valueFieldName, nestedSourceType) });
			var instance = Activator.CreateInstance(sourceType);
			sourceType.GetField(valueFieldName).SetValue(instance, Activator.CreateInstance(nestedSourceType));
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationModule = GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0));
			var destinationType = ConstructType(destinationModule, "MyClass", new[] { Tuple.Create(valueFieldName, nestedSourceType) });
			var nestedDestinationType = ConstructType(destinationModule, "MyOtherNestedClass", new Tuple<string, Type>[0]);
			Assert.Throws<TypeLoadException>(() =>
				ResolveDynamicAssembliesWhilePerformingAction(
					() => Deserialise(serialisedData, destinationType),
					destinationType,
					nestedDestinationType
				)
			);
		}

		/// <summary>
		/// If an older version of a type is serialised and then deserialised when a newer version of that type is loaded, if the newer version has any fields that do not
		/// have data in the serialised data then deserialisation should fail. Otherwise, if there was an immutable type that is normally initialised via a constructor that
		/// sets all properties to non-null properties them consumers of that type may reasonably expect all of the properties to be non-null but this would not be the case
		/// if deserialising from data that does not have information for some of those fields.
		/// </summary>
		[Fact]
		public static void DeserialisationWillFailIfAnyFieldsHaveNoData()
		{
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new Tuple<string, Type>[0]);
			var instance = Activator.CreateInstance(sourceType);
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				new[] { Tuple.Create("Id", typeof(int)) }
			);
			Assert.Throws<FieldNotPresentInSerialisedDataException>(() =>
				ResolveDynamicAssembliesWhilePerformingAction(
					() => Deserialise(serialisedData, destinationType),
					destinationType
				)
			);
		}

		/// <summary>
		/// If a type is serialised with a 'Name' field and then deserialised to a version of that type where 'Name' has [NonSerialized] on it then the 'Name' field should
		/// not be set when its desereialised, even though it's present in the serialised data
		/// </summary>
		[Fact]
		public static void DoNotSetFieldInDeserialisationIfItHasNonSerializedAttributeEvenIfItIsPresentInSerialisedData()
		{
			const string nameFieldName = "Name";
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new[] { Tuple.Create(nameFieldName, typeof(string)) });
			var instance = Activator.CreateInstance(sourceType);
			var nameFieldOnSource = sourceType.GetField(nameFieldName);
			nameFieldOnSource.SetValue(instance, "Test");
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Need to define the field using this lambda rather than specifying it through the fields argument because we need to set the custom attribute on it
					var fieldBuilder = typeBuilder.DefineField(nameFieldName, typeof(string), FieldAttributes.Public);
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(NonSerializedAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
				}
			);
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			var nameFieldOnDestination = destinationType.GetField(nameFieldName);
			Assert.Null(nameFieldOnDestination.GetValue(clone));
		}

		/// <summary>
		/// If deserialising data generated from an older version of a type that has a field that has been renamed, this would normally fail as it would not be possible to set a value on the
		/// renamed field in the newer version of the type. However, if the newer version has a property whose name and type match the old field and whose getter returns the value of the new
		/// field and whose setter sets the value of the new field and that property has a Deprecated attribute on it with a ReplacedBy value matching the name of the new field then the new
		/// type can be fully initialised and the new field will be set using the old field's data (though it will be set indirectly, via property setter).
		/// </summary>
		[Fact]
		public static void AllowDeserialisationOfOldDataTypeIfDeprecatedFieldsAreMappedOnToTheirReplacements()
		{
			var sourceType = ConstructType(GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)), "MyClass", new[] { Tuple.Create("NameOld", typeof(string)) });
			var instance = Activator.CreateInstance(sourceType);
			var nameFieldOnSource = sourceType.GetField("NameOld");
			nameFieldOnSource.SetValue(instance, "Test");
			var serialisedData = BinarySerialisation.Serialise(instance);

			var destinationType = ConstructType(
				GetModuleBuilder("DynamicAssemblyFor" + GetMyName(), new Version(1, 0)),
				"MyClass",
				fields: new Tuple<string, Type>[0],
				optionalFinisher: typeBuilder =>
				{
					// Need to define the field using this lambda rather than specifying it through the fields argument because we need the reference to build the property
					var nameNewfieldBuilder = typeBuilder.DefineField("NameNew", typeof(string), FieldAttributes.Public);

					// The destinationType has a field "NameNew" that replaces the "NameOld" field on the sourceType. To communicate this to the serialiser, the destinationType also has
					// a "NameOld property whose getter returns the "NameNew" value and whose setter sets the "NameNew" value and the property has [Deprecated(ReplacedBy: "NameNew")] on
					// it. When data serialised from the sourceType is deserialised as the destinationType, the "NameOld" value in the serialised data will be used to set the "NameNew"
					// property and the destinationType will be successfully initialised (even though the "NameNew" field was not set directly).
					var propertyBuilder = typeBuilder.DefineProperty("NameOld", PropertyAttributes.None, typeof(string), parameterTypes: Type.EmptyTypes);
					var getterBuilder = typeBuilder.DefineMethod("get_NameOld", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), parameterTypes: Type.EmptyTypes);
					var ilGeneratorForGetter = getterBuilder.GetILGenerator();
					ilGeneratorForGetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForGetter.Emit(OpCodes.Ldfld, nameNewfieldBuilder);
					ilGeneratorForGetter.Emit(OpCodes.Ret);
					propertyBuilder.SetGetMethod(getterBuilder);
					var setterBuilder = typeBuilder.DefineMethod("set_NameOld", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), parameterTypes: new[] { typeof(string) });
					var ilGeneratorForSetter = setterBuilder.GetILGenerator();
					ilGeneratorForSetter.Emit(OpCodes.Ldarg_0);
					ilGeneratorForSetter.Emit(OpCodes.Ldarg_1);
					ilGeneratorForSetter.Emit(OpCodes.Stfld, nameNewfieldBuilder);
					ilGeneratorForSetter.Emit(OpCodes.Ret);
					propertyBuilder.SetSetMethod(setterBuilder);
					propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(DeprecatedAttribute).GetConstructor(new[] { typeof(string) }), new[] { "NameNew" }));
				}
			);
			var clone = ResolveDynamicAssembliesWhilePerformingAction(
				() => Deserialise(serialisedData, destinationType),
				destinationType
			);
			Assert.Equal("Test", destinationType.GetField("NameNew").GetValue(clone));
		}

		/// <summary>
		/// Use the CallerMemberName attribute so that a method can gets its own name so that it can specify a descriptive dynamic assembly name (could have used nameof
		/// but that would invite copy-paste errors when new methods were added - the method name need to be changed AND the reference to it within the nameof call)
		/// </summary>
		private static string GetMyName([CallerMemberName] string callerName = null)
		{
			return callerName;
		}

		/// <summary>
		/// This calls the BinarySerialisationCloner Deserialise method but populates the generic type parameter with a runtime type value
		/// </summary>
		private static object Deserialise(byte[] serialisedData, Type type, IDeserialisationTypeConverter[] optionalTypeConverters = null)
		{
			var genericDeserialiseMethod = typeof(BinarySerialisation).GetMethod(nameof(Deserialise), new[] { typeof(byte[]), typeof(IDeserialisationTypeConverter[]) }).MakeGenericMethod(type);
			try
			{
				return genericDeserialiseMethod.Invoke(null, new object[] { serialisedData, optionalTypeConverters ?? new IDeserialisationTypeConverter[0] });
			}
			catch (TargetInvocationException e)
			{
				// If an exception is thrown within the "Invoke" call then it will be wrapped in a TargetInvocationException - this isn't very helpful, the tests above
				// that expect exceptions should be able to specify what exception should occur and not worry about unwrapping it from the TargetInvocationException.
				// To avoid that, the unwrapping is done here.
				if (e.InnerException != null)
					throw e.InnerException;
				throw;
			}
		}

		private static T CloneAndSupportDynamicAssemblies<T>(T value, params Type[] dynamicTypes)
		{
			// SupportReferenceReUseInMostlyTreeLikeStructure seems to be the best general compromise so we'll use that here
			return ResolveDynamicAssembliesWhilePerformingAction<T>(
				() => BinarySerialisationCloner.Clone(value, ReferenceReuseOptions.SupportReferenceReUseInMostlyTreeLikeStructure),
				dynamicTypes
			);
		}
	}
}