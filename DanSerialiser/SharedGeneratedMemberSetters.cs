using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DanSerialiser.Reflection;

namespace DanSerialiser
{
	internal static class SharedGeneratedMemberSetters
	{
		private static readonly Type _writerType = typeof(BinarySerialisationWriter);
		private static readonly MethodInfo writeByteMethod = _writerType.GetMethod("WriteByte", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte) }, null);
		private static readonly MethodInfo writeBytesMethod = _writerType.GetMethod("WriteBytes", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte[]) }, null);

		public static Action<object, BinarySerialisationWriter> TryToGenerateMemberSetter(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (writeByteMethod == null)
				throw new Exception("Unable to identify writer method 'WriteByte'");
			if (writeBytesMethod == null)
				throw new Exception("Unable to identify writer method 'WriteBytes'");

			// If there are any fields or properties whose types don't match the TypeWillWorkWithTypeGenerator conditions then don't try to make a type generator (there will be
			// potential complications such as checking for circular / reused references that can't be handled by a simple type generator)
			var fields = DefaultTypeAnalyser.Instance.GetAllFieldsThatShouldBeSet(type);

			var sourceParameter = Expression.Parameter(typeof(object), "untypedSource");
			var writerParameter = Expression.Parameter(_writerType, "writer");
			var typedSource = Expression.Variable(type, "source");
			var statements = new List<Expression>
			{
				Expression.Assign(typedSource, Expression.Convert(sourceParameter, typedSource.Type))
			};
			foreach (var field in fields)
			{
				var fieldNameBytes = BinarySerialisationWriterCachedNames.GetFieldNameBytesIfWantoSerialiseField(field, type)?.OnlyAsReferenceID;
				if (fieldNameBytes == null)
					return null;

				// TODO: Support Nullable<T> somehow..? Specific ones or ALL of them??! Can't do ALL cos object reference could screw things up, so maybe just standard ones?

				var methodArguments = new[] { field.FieldType };
				string fieldWriterMethodName;
				if (field.FieldType == typeof(Boolean))
					fieldWriterMethodName = "Boolean";
				else if (field.FieldType == typeof(Byte))
					fieldWriterMethodName = "Byte";
				else if (field.FieldType == typeof(SByte))
					fieldWriterMethodName = "SByte";
				else if (field.FieldType == typeof(Int16))
					fieldWriterMethodName = "Int16";
				else if (field.FieldType == typeof(Int32))
					fieldWriterMethodName = "Int32";
				else if (field.FieldType == typeof(Int64))
					fieldWriterMethodName = "Int64";

				// TODO: Other types (byte, int, etc..)

				else if (field.FieldType == typeof(Single))
					fieldWriterMethodName = "Single";
				else if (field.FieldType == typeof(Double))
					fieldWriterMethodName = "Double";
				else if (field.FieldType == typeof(Decimal))
					fieldWriterMethodName = "Decimal";
				else if (field.FieldType == typeof(Char))
					fieldWriterMethodName = "Char";
				else if (field.FieldType == typeof(String))
					fieldWriterMethodName = "String";
				else
					return null;

				var fieldWriterMethod = _writerType.GetMethod(fieldWriterMethodName, methodArguments);
				if (fieldWriterMethod == null)
					throw new Exception("Unable to identify writer method '" + fieldWriterMethodName + "'");

				statements.Add(
					Expression.Call(
						writerParameter,
						writeBytesMethod,
						Expression.Constant(new[] { (byte)BinarySerialisationDataType.FieldName }.Concat(fieldNameBytes).ToArray())
					)
				);
				statements.Add(
					Expression.Call(
						writerParameter,
						fieldWriterMethod,
						Expression.MakeMemberAccess(typedSource, field)
					)
				);
			}
			return
				Expression.Lambda<Action<object, BinarySerialisationWriter>>(
					Expression.Block(new[] { typedSource }, statements),
					sourceParameter,
					writerParameter
				)
				.Compile();
		}
	}
}