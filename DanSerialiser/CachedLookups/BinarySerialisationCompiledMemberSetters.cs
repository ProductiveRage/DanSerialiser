using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DanSerialiser.Reflection;
using static DanSerialiser.CachedLookups.BinarySerialisationWriterCachedNames;

namespace DanSerialiser.CachedLookups
{
	internal static class BinarySerialisationCompiledMemberSetters
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
			_writeArrayStartMethod, _writeArrayEndMethod,
			_writeNullMethod;
		static BinarySerialisationCompiledMemberSetters()
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
			_writeNullMethod = _writerType.GetMethod(nameof(BinarySerialisationWriter.Null), Type.EmptyTypes) ?? throw new Exception("Could not find " + nameof(BinarySerialisationWriter.Null));
		}

		/// <summary>
		/// This may return null if no ValueWriter is available
		/// </summary>
		public delegate ValueWriter ValueWriterRetriever(Type type);

		/// <summary>
		/// A member setter is a delegate that takes an instance and writes the data for fields and properties to a BinarySerialisationWriter. Note that it will not write the
		/// ObjectStart and ObjectEnd data because the caller should be responsible for tracking references (if the caller is configured to track references), which is tied to
		/// the ObjectStart data. If the caller is tracking references (to either reuse references that appear multiple times in the source data or to identify any circular
		/// references while performing serialisation) then member setters should only be generated for types whose fields and properties are types that do not support reference
		/// reuse, such as primitives and strings. Fields and properties that are primitives or strings or DateTime or TimeSpan or GUIDs (or one-dimensional arrays of any of those
		/// types) can be handled entirely by code within this method but it's also possible to provide member setters for other field or property types via the valueWriterRetriever
		/// argument - again, if the caller is tracking references then it should only pass through member setters via valueWriterRetriever that are for non-reference types, such as
		/// structs) because generating nested member setters in this manner will prevent any reference tracking.
		/// </summary>
		public static MemberSetterDetails TryToGenerateMemberSetter(Type type, IAnalyseTypesForSerialisation typeAnalyser, ValueWriterRetriever valueWriterRetriever)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (typeAnalyser == null)
				throw new ArgumentNullException(nameof(typeAnalyser));
			if (valueWriterRetriever == null)
				throw new ArgumentNullException(nameof(valueWriterRetriever));

			var sourceParameter = Expression.Parameter(type, "source");
			var writerParameter = Expression.Parameter(_writerType, "writer");
			var statements = new List<Expression>();
			var fieldsSet = new List<CachedNameData>();
			var (fields, properties) = typeAnalyser.GetFieldsAndProperties(type);
			var membersToConsiderSerialising = 
				fields.Select(f =>
				{
					var fieldNameBytes = GetFieldNameBytesIfWantoSerialiseField(f.Member, type);
					if (fieldNameBytes == null)
						return null;
					return new
					{
						FieldNameBytes = fieldNameBytes,
						ValueWriterIfPossibleToGenerate = TryToGetValueWriterForMember(f.Member, f.Member.FieldType, sourceParameter, writerParameter, valueWriterRetriever)
					};
				})
				.Concat(
					properties.Select(p =>
					{
						var fieldNameBytes = GetFieldNameBytesIfWantoSerialiseProperty(p.Member);
						if (fieldNameBytes == null)
							return null;
						return new
						{
							FieldNameBytes = fieldNameBytes,
							ValueWriterIfPossibleToGenerate = TryToGetValueWriterForMember(p.Member, p.Member.PropertyType, sourceParameter, writerParameter, valueWriterRetriever)
						};
					})
				)
				.Where(member => member != null); // Ignore any fields or properties that we didn't want to serialise
			foreach (var member in membersToConsiderSerialising)
			{
				// If it wasn't possible to get a member setter for this field/property type then it's not possible to generate a member setter for the type that it's on
				if (member.ValueWriterIfPossibleToGenerate == null)
					return null;

				// Generate the write-FieldName-to-stream method call
				statements.Add(
					Expression.Call(
						writerParameter,
						_writeBytesMethod,
						Expression.Constant(new[] { (byte)BinarySerialisationDataType.FieldName }.Concat(member.FieldNameBytes.OnlyAsReferenceID).ToArray())
					)
				);

				// Write out the value-serialising expression
				statements.Add(member.ValueWriterIfPossibleToGenerate);
				fieldsSet.Add(member.FieldNameBytes);
			}

			// Group all of the field setters together into one call
			Expression body;
			if (statements.Count == 0)
				body = Expression.Empty();
			else if (statements.Count == 1)
				body = statements[0];
			else
				body = Expression.Block(statements);
			return new MemberSetterDetails(
				type,
				Expression.Lambda(
					body,
					sourceParameter,
					writerParameter
				),
				GetTypeNameBytes(type),
				fieldsSet.ToArray()
			);
		}

		private static Expression TryToGetValueWriterForMember(MemberInfo member, Type memberType, ParameterExpression typedSource, ParameterExpression writerParameter, ValueWriterRetriever valueWriterRetriever)
		{
			if (memberType.IsArray && (memberType.GetArrayRank() == 1))
			{
				// Note: There is some duplication of logic between here and the Serialiser class - namely that we call ArrayStart, then write the elements, then call ArrayEnd
				var elementType = memberType.GetElementType();
				var current = Expression.Parameter(elementType, "current");
				var valueWriterForFieldElementType = TryToGetValueWriterForType(elementType, current, writerParameter, valueWriterRetriever);
				if (valueWriterForFieldElementType == null)
					return null;

				var arrayValue = Expression.MakeMemberAccess(typedSource, member);
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
					Expression.ReferenceEqual(arrayValue, Expression.Constant(null, memberType)),
					ifNull,
					ifNotNull
				);
			}

			if (memberType.IsEnum)
			{
				// Note: There is some duplication of logic between here and the Serialiser class (that we write the underlying value)
				var underlyingType = memberType.GetEnumUnderlyingType();
				var enumValue = Expression.MakeMemberAccess(typedSource, member);
				return TryToGetValueWriterForType(
					underlyingType,
					Expression.Convert(enumValue, underlyingType),
					writerParameter,
					valueWriterRetriever
				);
			}

			return TryToGetValueWriterForType(memberType, Expression.MakeMemberAccess(typedSource, member), writerParameter, valueWriterRetriever);
		}

		private static Expression TryToGetValueWriterForType(Type type, Expression value, ParameterExpression writerParameter, ValueWriterRetriever valueWriterRetriever)
		{
			// Try to get a BinarySerialisationWriter method to call to serialise the value
			// - If it's a Nullable then unwrap the underlying type and try to find a method for that and then we'll have to include some null-checking to the member setter
			//   if we identify a method that will write out the underlying type
			var nullableTypeInner = GetUnderlyingNullableTypeIfApplicable(type);
			var fieldWriterMethod = TryToGetWriterMethodToSerialiseType(nullableTypeInner ?? type);
			Expression individualMemberSetterForNonNullValue;
			if (fieldWriterMethod != null)
			{
				// When writing a non-null value from a Nullable<> then we need to write the underlying value - Nullable<T> gets magic treatment from the compiler and we
				// want to achieve the same sort of thing, which means that (in cases where the Nullable<T> is not null) we write the value AS THE UNDERLYING TYPE into
				// the field (we DON'T write the value AS NULLABLE<T> into the field). If we're not dealing with a Nullable<T> then we CAN use the value to directly
				// set the field.
				individualMemberSetterForNonNullValue = Expression.Call(
					writerParameter,
					fieldWriterMethod,
					(nullableTypeInner == null) ? value : Expression.Convert(value, nullableTypeInner)
				);
			}
			else
			{
				// Since we weren't able to find a BinarySerialisationWriter method for the value, see if the valueWriterRetriever lookup can provide a member setter that
				// was generated earlier. If not then we're out of options and have to give up. If we CAN get one then we'll use that (we'll have to bookend it with Object
				// Start and End calls so that it describes the complete data for a non-reference-tracked instance)
				var preBuiltMemberSetter = valueWriterRetriever(nullableTypeInner ?? type);
				if (preBuiltMemberSetter == null)
					return null;

				// Instead of calling writer.ObjectStart(type)/ObjectEnd() when writing out the data for complete objects, we'll write out the data in the correct format here
				// because it will be cheaper as we the ObjectStart method has to determine whether the type has appeared already in the content and, if not, write out the full
				// type name string and, if it HAS been encountered already, write out just the Name Reference ID - here, we can skip this check and always go straight for the
				// Name Reference ID approach, which will save us many dictionary lookups if the object graph is large. The reason that we can skip the write-name-or-id check
				// is that member setters created here will be used in one of two situations:
				//  1. During a regular serialisation (ie. any ReferenceReuseOptions other than SpeedyButLimited) and only after an instance of the type has been serialised the
				//     slower way (through the Serialiser class), meaning that the type name has definitely been encountered already
				//  2. During a SpeedyButLimited serialisation, in which case TypeNamePreLoad data will be emitted at the start of the serialisation data and we can be sure,
				//     then, that the type name will have been encountered by the reader before it tries to deserialise any instances of the type (because TypeNamePreLoad
				//     and FieldNamePreLoad data must always be the first content in serialised data)
				if (preBuiltMemberSetter.SetToDefault)
				{
					// This means that we want to record serialisation data that will set this value to default(T)
					//  - If we're looking at a reference type then that means that we will want to call "writer.Null()" when it comes to writing this value (in this case,
					//    we can return the expression to do that immediately; if we don't return now then this method will wrap this expression so that it becomes a block
					//    that says "when serialising, if the source value is null then calling writer.Null() but if the source value is non-null then also call write.Null()"
					//    and that is a waste of time!)
					//  - If we're looking at a value type then we need to write the ObjectStart ObjectEnd data so that it writes content for a struct in its default state
					if (!type.IsValueType)
						return Expression.Call(writerParameter, _writeNullMethod);

					var typeNameBytes = GetTypeNameBytes(type);
					var bytesForEmptyObjectInitialisation = new List<byte> { (byte)BinarySerialisationDataType.ObjectStart };
					bytesForEmptyObjectInitialisation.AddRange(typeNameBytes.OnlyAsReferenceID);
					bytesForEmptyObjectInitialisation.Add((byte)BinarySerialisationDataType.ObjectEnd);
					individualMemberSetterForNonNullValue = Expression.Call(
						writerParameter,
						_writeBytesMethod,
						Expression.Constant(bytesForEmptyObjectInitialisation.ToArray())
					);
				}
				else
				{
					var typeNameBytes = preBuiltMemberSetter.OptionalTypeNameOverideIfNotSettingToDefault ?? GetTypeNameBytes(type);
					var bytesForObjectStart = new List<byte> { (byte)BinarySerialisationDataType.ObjectStart };
					bytesForObjectStart.AddRange(typeNameBytes.OnlyAsReferenceID);
					individualMemberSetterForNonNullValue = Expression.Block(
						Expression.Call(writerParameter, _writeBytesMethod, Expression.Constant(bytesForObjectStart.ToArray())),
						Expression.Invoke(
							preBuiltMemberSetter.MemberSetterIfNotSettingToDefault,
							(nullableTypeInner == null) ? value : Expression.Convert(value, nullableTypeInner),
							writerParameter
						),
						Expression.Call(writerParameter, _writeByteMethod, Expression.Constant((byte)BinarySerialisationDataType.ObjectEnd))
					);
				}
			}

			// If we're not dealing with a type that may be null then there is nothing more to do!
			if (type.IsValueType && (nullableTypeInner == null))
				return individualMemberSetterForNonNullValue;

			// If we ARE dealing with a may-be-null value (a Nullable<T> or reference type) then the member-setting expression needs wrapping in an if-null-call-"Null"-
			// method-on-BinarySerialisationWriter check
			// 2018-07-01: The "Null" method on IWrite was added because it's cheaper to write a single Null byte. Otherwise, a null Object would have been written as
			// two for ObjectStart, ObjectEnd; a null Array would have been written as be an ArrayStart byte, the bytes for a null String (for the element type) and
			// an ArrayEnd; a null String would have required three bytes to specify the String data type and then a length of minus one encoded as an Int32_16.
			var ifNullCheck = type.IsValueType
				? Expression.Equal(value, Expression.Constant(null, type)) // Null check for structs such as Nullable<T> (this will have the op_Equality or Equals implementations)
				: Expression.ReferenceEqual(value, Expression.Constant(null, type)); // Null check for reference types, should be quicker to compare it to a null reference than let it implement custom equality logic
			return Expression.IfThenElse(
				ifNullCheck,
				Expression.Call(writerParameter, nameof(BinarySerialisationWriter.Null), typeArguments: Type.EmptyTypes),
				individualMemberSetterForNonNullValue
			);
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

		/// <summary>
		/// This is used in scenarios where existing member setters for types are used to construct member setters for more complex types, whose fields or properties include types that
		/// we have already generated member setters for. When the TryToGenerateMemberSetter methods requests an instance of this class for a particular type, a null reference indicates
		/// that we have no idea what to do and it will not be possible to create a member setter for the type that had a field or property of the type that was just requested (in other
		/// words, we don't know what to do and so we can't do anything). An instance of this that has a SetToDefault value of true indicates that the field or property of the requested
		/// type should be set to the default value for that type (meaning that we DO know what to do with the field or property and that is to set it to null / struct default-value).
		/// An instance of this that has a SetToDefault value of false will always have a non-null MemberSetterIfNotSettingToDefault reference and this will be a member setter Lamda-
		/// Expression (an expression that emits the serialisation data for the properties of an instance of the requested type).
		/// </summary>
		public sealed class ValueWriter
		{
			public static ValueWriter OverrideValue(CachedNameData typeNameOveride, LambdaExpression memberSetter)
			{
				return new ValueWriter(
					false,
					memberSetter ?? throw new ArgumentNullException(nameof(memberSetter)),
					typeNameOveride ?? throw new ArgumentNullException(nameof(typeNameOveride))
				);
			}
			public static ValueWriter PopulateValue(LambdaExpression memberSetter) => new ValueWriter(false, memberSetter ?? throw new ArgumentNullException(nameof(memberSetter)), null);
			public static ValueWriter SetValueToDefault { get; } = new ValueWriter(true, null, null);
			private ValueWriter(bool setToDefault, LambdaExpression memberSetterIfNotSettingToDefault, CachedNameData optionalTypeNameOverideIfNotSettingToDefault)
			{
				SetToDefault = setToDefault;
				MemberSetterIfNotSettingToDefault = memberSetterIfNotSettingToDefault;
				OptionalTypeNameOverideIfNotSettingToDefault = optionalTypeNameOverideIfNotSettingToDefault;
			}

			public bool SetToDefault { get; }

			/// <summary>
			/// This will be null if SetToDefault is true and non-null if SetToDefault is false
			/// </summary>
			public LambdaExpression MemberSetterIfNotSettingToDefault { get; }

			/// <summary>
			/// This will always be null if SetToDefault is true and may be null or non-null if SetToDefault is false (if non-null then this data should be used to write the
			/// ObjectStart header instead of writing type name data corresponding the source type - this will be used when using type converters to alter data in transit
			/// </summary>
			public CachedNameData OptionalTypeNameOverideIfNotSettingToDefault { get; }
		}
	}
}