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
		private static readonly MethodInfo _writeByteMethod = _writerType.GetMethod("WriteByte", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte) }, null);
		private static readonly MethodInfo _writeBytesMethod = _writerType.GetMethod("WriteBytes", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte[]) }, null);
		public static Action<object, BinarySerialisationWriter> TryToGenerateMemberSetter(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (_writeByteMethod == null)
				throw new Exception("Unable to identify writer method 'WriteByte'");
			if (_writeBytesMethod == null)
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

				// Try to get a BinarySerialisationWriter method to call to serialise the value (if it's a Nullable then unwrap the underlying type and try to find a method
				// for that - we'll have to include some null-checking to the member setter if we do this, see a litte further down..)
				var nullableTypeInner = GetUnderlyingNullableTypeIfApplicable(field.FieldType);
				var fieldWriterMethod = TryToGetWriterMethodToSerialiseType(nullableTypeInner ?? field.FieldType);
				if (fieldWriterMethod == null)
					return null;

				// Generate the write-FieldName-to-stream method call
				statements.Add(
					Expression.Call(
						writerParameter,
						_writeBytesMethod,
						Expression.Constant(new[] { (byte)BinarySerialisationDataType.FieldName }.Concat(fieldNameBytes).ToArray())
					)
				);

				// Generate the write-Field-value-to-stream method call
				if (nullableTypeInner != null)
				{
					// If this is a Nullable value then we need to check for is-null and then either write a null object or write the underlying value (Nullable<T> gets magic
					// treatment by the compiler and so we don't have to write the data as a Nullable<T>, we can write either null or T)
					statements.Add(Expression.IfThenElse(
						test: Expression.Equal(
							Expression.MakeMemberAccess(typedSource, field),
							Expression.Constant(null, field.FieldType)
						),
						ifTrue: Expression.Block(
							Expression.Call(
								writerParameter,
								nameof(BinarySerialisationWriter.ObjectStart),
								typeArguments: new[] { typeof(object) },
								arguments: new[] { Expression.Constant(null, typeof(object)) }
							),
							Expression.Call(
								writerParameter,
								nameof(BinarySerialisationWriter.ObjectEnd),
								typeArguments: Type.EmptyTypes
							)
						),
						ifFalse: Expression.Call(
							writerParameter,
							fieldWriterMethod,
							Expression.Convert(
								Expression.MakeMemberAccess(typedSource, field),
								nullableTypeInner
							)
						)
					));
				}
				else
				{
					// For non-Nullable values, we just write the value straight out using the identifier writer method
					statements.Add(Expression.Call(
						writerParameter,
						fieldWriterMethod,
						Expression.MakeMemberAccess(typedSource, field)
					));
				}
			}

			// Group all of the field setters together into one call
			return
				Expression.Lambda<Action<object, BinarySerialisationWriter>>(
					Expression.Block(new[] { typedSource }, statements),
					sourceParameter,
					writerParameter
				)
				.Compile();
		}

		private static Type GetUnderlyingNullableTypeIfApplicable(Type type)
		{
			return (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
				? type.GetGenericArguments()[0]
				: null;
		}

		private static MethodInfo TryToGetWriterMethodToSerialiseType(Type type)
		{
			string fieldWriterMethodName;
			if (type == CommonTypeOfs.Boolean)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Boolean);
			else if (type == CommonTypeOfs.Byte)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Byte);
			else if (type == CommonTypeOfs.SByte)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.SByte);
			else if (type == CommonTypeOfs.Int16)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Int16);
			else if (type == CommonTypeOfs.Int32)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Int32);
			else if (type == CommonTypeOfs.Int64)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Int64);
			else if (type == CommonTypeOfs.UInt16)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.UInt16);
			else if (type == CommonTypeOfs.UInt32)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.UInt32);
			else if (type == CommonTypeOfs.UInt64)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.UInt64);
			else if (type == CommonTypeOfs.Single)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Single);
			else if (type == CommonTypeOfs.Double)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Double);
			else if (type == CommonTypeOfs.Decimal)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Decimal);
			else if (type == CommonTypeOfs.Char)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.Char);
			else if (type == CommonTypeOfs.String)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.String);
			else if (type == CommonTypeOfs.DateTime)
				fieldWriterMethodName = nameof(BinarySerialisationWriter.DateTime);
			else
				return null;

			var fieldWriterMethod = _writerType.GetMethod(fieldWriterMethodName, new[] { type });
			if (fieldWriterMethod == null)
				throw new Exception("Unable to identify writer method '" + fieldWriterMethodName + "'");
			return fieldWriterMethod;
		}
	}
}