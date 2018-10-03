using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DanSerialiser.Reflection;

namespace DanSerialiser.CachedLookups
{
	internal static class SharedGeneratedMemberSetters
	{
		private static readonly Type _writerType = typeof(BinarySerialisationWriter);
		private static readonly MethodInfo
			_writeByteMethod, _writeBytesMethod,
			_writeBooleanValueMethod,
			_writeByteValueMethod, _writeSByteValueMethod,
			_writeInt16ValueMethod, _writeInt32ValueMethod, _writeInt64ValueMethod, _writeUInt16ValueMethod, _writeUInt32ValueMethod, _writeUInt64ValueMethod,
			_writeSingleValueMethod, _writeDoubleValueMethod, _writeDecimalValueMethod,
			_writeDateTimeValueMethod, _writeTimeSpanValueMethod,
			_writeCharValueMethod, _writeStringValueMethod,
			_writeGuidValueMethod,
			_writeArrayStartMethod, _writeArrayEndMethod;
		static SharedGeneratedMemberSetters()
		{
			_writerType = typeof(BinarySerialisationWriter);

			// Private methods (these write bytes directly, without headers)
			_writeByteMethod = _writerType.GetMethod("WriteByte", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte) }, null) ?? throw new Exception("Could not find 'WriteByte'");
			_writeBytesMethod = _writerType.GetMethod("WriteBytes", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(byte[]) }, null) ?? throw new Exception("Could not find 'WriteBytes'");

			// Public methods (these write header bytes and then the serialised value)
			_writeBooleanValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Boolean), new[] { typeof(Boolean) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Boolean));
			_writeByteValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Byte), new[] { typeof(Byte) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Byte));
			_writeSByteValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.SByte), new[] { typeof(SByte) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.SByte));
			_writeInt16ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Int16), new[] { typeof(Int16) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Int16));
			_writeInt32ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Int32), new[] { typeof(Int32) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Int32));
			_writeInt64ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Int64), new[] { typeof(Int64) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Int64));
			_writeUInt16ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.UInt16), new[] { typeof(UInt16) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.UInt16));
			_writeUInt32ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.UInt32), new[] { typeof(UInt32) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.UInt32));
			_writeUInt64ValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.UInt64), new[] { typeof(UInt64) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.UInt64));
			_writeSingleValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Single), new[] { typeof(Single) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Single));
			_writeDoubleValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Double), new[] { typeof(Double) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Double));
			_writeDecimalValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Decimal), new[] { typeof(Decimal) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Decimal));
			_writeDateTimeValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.DateTime), new[] { typeof(DateTime) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.DateTime));
			_writeTimeSpanValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.TimeSpan), new[] { typeof(TimeSpan) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.TimeSpan));
			_writeCharValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Char), new[] { typeof(Char) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Char));
			_writeStringValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.String), new[] { typeof(String) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.String));
			_writeGuidValueMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Guid), new[] { typeof(Guid) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Guid));
			_writeArrayStartMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.ArrayStart), new[] { typeof(object), typeof(Type) }) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.ArrayStart));
			_writeArrayEndMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.ArrayEnd), Type.EmptyTypes) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.ArrayEnd));
		}

		public static Action<object, BinarySerialisationWriter> TryToGenerateMemberSetter(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

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

				var valueWriterIfPossibleToGenerate = TryToGetValueWriterForField(field, typedSource, writerParameter);
				if (valueWriterIfPossibleToGenerate == null)
					return null;

				// Generate the write-FieldName-to-stream method call
				statements.Add(
					Expression.Call(
						writerParameter,
						_writeBytesMethod,
						Expression.Constant(new[] { (byte)BinarySerialisationDataType.FieldName }.Concat(fieldNameBytes).ToArray())
					)
				);

				// Write out the value-serialising expression
				statements.Add(valueWriterIfPossibleToGenerate);
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

		private static Expression TryToGetValueWriterForField(FieldInfo field, ParameterExpression typedSource, ParameterExpression writerParameter)
		{
			if (field.FieldType.IsArray && (field.FieldType.GetArrayRank() == 1))
			{
				// Note: There is some duplication of logic between here and the Serialiser class - namely that we call ArrayStart, then write the elements, then call ArrayEnd
				var elementType = field.FieldType.GetElementType();
				var current = Expression.Parameter(elementType, "current");
				var valueWriterForFieldElementType = TryToGetValueWriterForType(elementType, current, writerParameter);
				if (valueWriterForFieldElementType == null)
					return null;

				var arrayValue = Expression.MakeMemberAccess(typedSource, field);
				var ifNull = Expression.Call(
					writerParameter,
					nameof(BinarySerialisationWriter.Null),
					typeArguments: Type.EmptyTypes
				);
				var arrayLength = Expression.MakeMemberAccess(arrayValue, typeof(Array).GetProperty(nameof(Array.Length)));
				var index = Expression.Parameter(typeof(int), "i");
				var breakLabel = Expression.Label("break");
				var ifNotNull = Expression.Block(
					Expression.Call(writerParameter, _writeArrayStartMethod, arrayValue, Expression.Constant(elementType)),
					Expression.Block(
						new[] { index, current },
						Expression.Assign(index, Expression.Constant(0)),
						Expression.Loop(
							Expression.IfThenElse(
								Expression.LessThan(index, arrayLength),
								Expression.Block(
									Expression.Assign(current, Expression.ArrayAccess(arrayValue, index)),
									valueWriterForFieldElementType,
									Expression.Assign(index, Expression.Add(index, Expression.Constant(1)))
								),
								Expression.Break(breakLabel)
							),
							breakLabel
						)
					),
					Expression.Call(writerParameter, _writeArrayEndMethod)
				);
				return Expression.IfThenElse(
					Expression.Equal(arrayValue, Expression.Constant(null, field.FieldType)),
					ifNull,
					ifNotNull
				);
			}

			if (field.FieldType.IsEnum)
			{
				// Note: There is some duplication of logic between here and the Serialiser class (that we write the underlying value)
				var underlyingType = field.FieldType.GetEnumUnderlyingType();
				var enumValue = Expression.MakeMemberAccess(typedSource, field);
				return TryToGetValueWriterForType(
					underlyingType,
					Expression.Convert(enumValue, underlyingType),
					writerParameter
				);
			}

			return TryToGetValueWriterForType(field.FieldType, Expression.MakeMemberAccess(typedSource, field), writerParameter);
		}

		private static Expression TryToGetValueWriterForType(Type type, Expression value, ParameterExpression writerParameter)
		{
			// Try to get a BinarySerialisationWriter method to call to serialise the value (if it's a Nullable then unwrap the underlying type and try to find a method
			// for that - we'll have to include some null-checking to the member setter if we do this, see a litte further down..)
			//if (field.FieldType.IsArray)
			var nullableTypeInner = GetUnderlyingNullableTypeIfApplicable(type);
			var fieldWriterMethod = TryToGetWriterMethodToSerialiseType(nullableTypeInner ?? type);
			if (fieldWriterMethod == null)
				return null;

			// Generate the write-Field-value-to-stream method call
			if ((nullableTypeInner != null) || !type.IsValueType)
			{
				// If this is a Nullable value then we need to check for is-null and then either write a null object or write the underlying value (Nullable<T> gets magic
				// treatment by the compiler and so we don't have to write the data as a Nullable<T>, we can write either null or T)
				// 2018-07-01: Now that there is a "Null" method on IWrite, we need to apply the same logic to reference type fields (it's cheaper to write a single Null
				// byte for a null Object - which would otherwise be two bytes of ObjectStart, ObjectEnd - or a null Array - which would otherwise be an ArrayStart byte,
				// the bytes for a null String and an ArrayEnd - or a null String - which would have required three bytes for the String data type and then a length of
				// minus one encoded as an Int32_16 data type and two bytes for a -1 length)
				var ifNull = Expression.Call(
					writerParameter,
					nameof(BinarySerialisationWriter.Null),
					typeArguments: Type.EmptyTypes
				);
				var ifNotNull = Expression.Call(
					writerParameter,
					fieldWriterMethod,
					Expression.Convert(
						value,
						nullableTypeInner ?? type
					)
				);
				return Expression.IfThenElse(
					Expression.Equal(value, Expression.Constant(null, type)),
					ifNull,
					ifNotNull
				);
			}
			else
			{
				// For non-Nullable values, we just write the value straight out using the identifier writer method
				return Expression.Call(
					writerParameter,
					fieldWriterMethod,
					value
				);
			}
		}

		private static Type GetUnderlyingNullableTypeIfApplicable(Type type)
		{
			return (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
				? type.GetGenericArguments()[0]
				: null;
		}

		private static MethodInfo TryToGetWriterMethodToSerialiseType(Type type)
		{
			if (type == CommonTypeOfs.Boolean)
				return _writeBooleanValueMethod;
			else if (type == CommonTypeOfs.Byte)
				return _writeByteValueMethod;
			else if (type == CommonTypeOfs.SByte)
				return _writeSByteValueMethod;
			else if (type == CommonTypeOfs.Int16)
				return _writeInt16ValueMethod;
			else if (type == CommonTypeOfs.Int32)
				return _writeInt32ValueMethod;
			else if (type == CommonTypeOfs.Int64)
				return _writeInt64ValueMethod;
			else if (type == CommonTypeOfs.UInt16)
				return _writeUInt16ValueMethod;
			else if (type == CommonTypeOfs.UInt32)
				return _writeUInt32ValueMethod;
			else if (type == CommonTypeOfs.UInt64)
				return _writeUInt64ValueMethod;
			else if (type == CommonTypeOfs.Single)
				return _writeSingleValueMethod;
			else if (type == CommonTypeOfs.Double)
				return _writeDoubleValueMethod;
			else if (type == CommonTypeOfs.Decimal)
				return _writeDecimalValueMethod;
			else if (type == CommonTypeOfs.Char)
				return _writeCharValueMethod;
			else if (type == CommonTypeOfs.String)
				return _writeStringValueMethod;
			else if (type == CommonTypeOfs.DateTime)
				return _writeDateTimeValueMethod;
			else if (type == CommonTypeOfs.TimeSpan)
				return _writeTimeSpanValueMethod;
			else if (type == CommonTypeOfs.Guid)
				return _writeGuidValueMethod;
			else
				return null;
		}
	}
}