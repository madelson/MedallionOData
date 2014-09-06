using Medallion.OData.Client;
using Medallion.OData.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
	/// <summary>
	/// Represents a node in the OData "language". This class also contains static factories
	/// for specific expression types, much like <see cref="System.Linq.Expressions.Expression"/>
	/// </summary>
	public abstract class ODataExpression
	{
		internal ODataExpression(ODataExpressionKind kind, ODataExpressionType type, Type clrType)
		{
            Throw.IfNull(clrType, "clrType");
            Throw.If(!type.IsCompatibleWith(clrType), "clrType must be compatible with kind");

			this.Kind = kind;
			this.Type = type;
            this.ClrType = clrType;
		}

        /// <summary>the expression kind</summary>
		public ODataExpressionKind Kind { get; private set; }
        /// <summary>the odata type of the expression</summary>
        public ODataExpressionType Type { get; private set; }
        /// <summary>the .NET type of the expression</summary>
        public Type ClrType { get; private set; }

        internal abstract string ToODataExpressionLanguage();

        /// <summary>
        /// Returns the OData expression language text for the expression
        /// </summary>
        public sealed override string ToString()
        {
            return this.ToODataExpressionLanguage();
        }

		#region ---- Static factories ----
        private static readonly IReadOnlyCollection<ODataExpressionType> NonNumericComparableTypes = new[] { ODataExpressionType.DateTime, ODataExpressionType.DateTimeOffset, ODataExpressionType.Time };
		internal static ODataBinaryOpExpression BinaryOp(ODataExpression left, ODataBinaryOp @operator, ODataExpression right)
		{
			Throw.IfNull(left, "left");
			Throw.IfNull(right, "right");

            // add in implicit conversions as needed
            ODataExpression convertedLeft, convertedRight;
            if ((left.Type == ODataExpressionType.Unknown || left.Type == ODataExpressionType.Null) 
                && (right.Type == ODataExpressionType.Unknown || right.Type == ODataExpressionType.Null))
            {
                var canonicalType = GetCanonicalArgumentType(@operator);
                if (canonicalType != typeof(ODataObject))
                {
                    convertedLeft = left.Type == ODataExpressionType.Unknown
                        ? ConvertFromUnknownType(left, canonicalType)
                        : LiftNullToUnknown(left);
                    convertedRight = right.Type == ODataExpressionType.Unknown
                        ? ConvertFromUnknownType(right, canonicalType)
                        : LiftNullToUnknown(right);
                }
                else 
                {
                    convertedLeft = left.Type == ODataExpressionType.Null ? LiftNullToUnknown(left) : left;
                    convertedRight = right.Type == ODataExpressionType.Null ? LiftNullToUnknown(right) : right;
                }
            }
            else if (left.Type == ODataExpressionType.Unknown)
            {
                convertedLeft = ConvertFromUnknownType(left, right.ClrType);
                convertedRight = right;
            }
            else if (right.Type == ODataExpressionType.Unknown)
            {
                convertedLeft = left;
                convertedRight = ConvertFromUnknownType(right, left.ClrType); ;
            }
			else if (left.Type == right.Type)
			{
                convertedLeft = left;
                convertedRight = right;
			}
			else if (left.Type.IsImplicityCastableTo(right.Type))
			{
                convertedLeft = Convert(left, right.ClrType);
                convertedRight = right;
			}
			else if (right.Type.IsImplicityCastableTo(left.Type))
			{
                convertedLeft = left;
                convertedRight = Convert(right, left.ClrType);
			}
            else
            {
                throw new ArgumentException(string.Format("Operator {0} cannot be applied to operands of type '{1}' and '{2}'", @operator, left.Type, right.Type));
            }

            // determine the result type
            var operandClrType = convertedLeft.ClrType;
            var operandType = convertedLeft.Type;
            Type resultClrType;
            switch (@operator)
            {
                case ODataBinaryOp.Add:
                case ODataBinaryOp.Subtract:
                case ODataBinaryOp.Multiply:
                case ODataBinaryOp.Divide:
                case ODataBinaryOp.Modulo:
                    Throw.If(!operandType.IsNumeric(), "Operator requires numeric type");
                    resultClrType = operandClrType;
                    break;
                case ODataBinaryOp.LessThan:
                case ODataBinaryOp.LessThanOrEqual:
                case ODataBinaryOp.GreaterThanOrEqual:
                case ODataBinaryOp.GreaterThan:
                    Throw.If(
                        !operandType.IsNumeric() && !NonNumericComparableTypes.Contains(operandType) && operandType != ODataExpressionType.Unknown, 
                        "Operator requires a type with comparison operators defined"
                    );
                    resultClrType = typeof(bool);
                    break;
                case ODataBinaryOp.And:
                case ODataBinaryOp.Or:
                    Throw.If(operandClrType != typeof(bool), "Boolean operators require operands with a non-nullable boolean type");
                    resultClrType = typeof(bool);
                    break;
                case ODataBinaryOp.Equal:
                case ODataBinaryOp.NotEqual:
                    resultClrType = typeof(bool);
                    break;
                default:
                    throw Throw.UnexpectedCase(@operator);
            }

            // construct the expression
            return new ODataBinaryOpExpression(convertedLeft, @operator, convertedRight, resultClrType);
		}

        internal static ODataUnaryOpExpression UnaryOp(ODataExpression operand, ODataUnaryOp @operator)
		{
			Throw.IfNull(operand, "operand");
			Throw.If(operand.Type != ODataExpressionType.Boolean && operand.Type != ODataExpressionType.Unknown, "operand: must be a boolean type");

            var finalOperand = operand.Type == ODataExpressionType.Unknown
                ? ConvertFromUnknownType(operand, typeof(bool))
                : operand;

			return new ODataUnaryOpExpression(finalOperand, @operator);
		}

        internal static ODataConvertExpression Convert(ODataExpression expression, Type clrType)
		{
			Throw.IfNull(expression, "expression");
            Throw.IfNull(clrType, "clrType");
            
            Throw<ArgumentException>.If(
                // can only convert where (1) a cast is valid or (2) a cast might succeed because the target type
                // derives from of the expression type
                expression.Type == ODataExpressionType.Unknown
                || (
                    !expression.ClrType.IsCastableTo(clrType)
                    && !clrType.IsAssignableFrom(expression.ClrType)
                ),
                () => "cannot convert from " + expression.ClrType + " to " + clrType
            );
			return new ODataConvertExpression(expression, clrType);
		}

        internal static ODataConstantExpression Constant(object value, Type clrType = null)
		{
            if (value == null)
            {
                if (clrType == null)
                {
                    return new ODataConstantExpression(null, ODataExpressionType.Null, typeof(object));
                }
                else
                {
                    Throw<ArgumentException>.If(!clrType.CanBeNull(), "Cannot create a null constant for non-nullable type " + clrType);
                    var oDataType = clrType.ToODataExpressionType();
                    return new ODataConstantExpression(null, clrType.ToODataExpressionType(), clrType);
                }
            }
            else
            {
                Throw.If(clrType == typeof(ODataObject), "clrType: cannot create an unknown constant with a value");

                var finalClrType = clrType ?? value.GetType();
                var oDataType = finalClrType.ToODataExpressionType();
                Throw<ArgumentException>.If(!oDataType.IsPrimitive() && oDataType != ODataExpressionType.Type, () => "Cannot create a non-null constant of non-primitive type " + finalClrType);
                Throw<ArgumentException>.If(
                    clrType != null 
                        && !(
                            clrType.IsInstanceOfType(value)
                            || value.GetType().IsImplicitlyCastableTo(clrType)
                        ),                    
                    () => string.Format("Cannot create a constant of type '{0}' with value '{1}'", clrType, value)
                );
                return new ODataConstantExpression(value, oDataType, finalClrType);
            }
		}

		private static readonly ILookup<ODataFunction, ODataFunctionAttribute> FunctionSignatures = Helpers.GetValuesAndFields<ODataFunction>()
			.SelectMany(t => t.Item2.GetCustomAttributes<ODataFunctionAttribute>(), (t, s) => new { val = t.Item1, sig = s })
			.ToLookup(
				t => t.val,
				t => t.sig
			);

        internal static ODataCallExpression Call(ODataFunction function, IEnumerable<ODataExpression> arguments)
		{
			var argumentsList = (arguments as IReadOnlyList<ODataExpression>) ?? arguments.ToArray();
			var signatures = FunctionSignatures[function];
			var matches =
				(from s in signatures
                let args = s.Arguments.Select((v, i) => new { Value = v, Index = i })
			    let score = s.Arguments.Count != argumentsList.Count ? default(int?)
                    : args.All(iv => !iv.Value.HasValue || iv.Value.Value == argumentsList[iv.Index].Type || argumentsList[iv.Index].Type == ODataExpressionType.Unknown) ? 0
                    : args.All(iv => !iv.Value.HasValue || argumentsList[iv.Index].Type.IsImplicityCastableTo(iv.Value.Value) || argumentsList[iv.Index].Type == ODataExpressionType.Unknown) ? 1
				    : default(int?)
				where score.HasValue
				orderby score
				select new { sig = s, score })
				.ToArray();
			Throw<ArgumentException>.If(matches.Length == 0, () => string.Format("No version of {0} matched the argument types [{1}]", function, arguments.Select(a => a.Type).ToDelimitedString(", ")));
			Throw<ArgumentException>.If(
                matches.Length > 1 && matches[0].score == matches[1].score && argumentsList.All(e => e.Type != ODataExpressionType.Unknown), 
                () => string.Format("Ambiguous match for argument types [{0}] between {1} and {2}", arguments.Select(a => a.Type).ToDelimitedString(", "), matches[0].sig, matches[1].sig)
            );

			var match = matches[0].sig;
			var castArguments = argumentsList.Select((a, i) =>
                    !match.Arguments[i].HasValue || a.Type == match.Arguments[i] ? a
                    : a.Type == ODataExpressionType.Unknown ? ConvertFromUnknownType(a, match.Arguments[i].Value.ToClrType()) 
					: Convert(a, match.Arguments[i].Value.ToClrType()))
				.ToList()
				.AsReadOnly();

			return new ODataCallExpression(
                function, 
                castArguments, 
                match.ReturnType.HasValue
                    ? match.ReturnType.Value.ToClrType()
                    // this case handles cast, whose return type is dynamic depending on its arguments
                    : (Type)((ODataConstantExpression)castArguments[1]).Value
            );
		}

        internal static ODataMemberAccessExpression MemberAccess(ODataMemberAccessExpression expression, PropertyInfo member)
		{
			Throw.IfNull(member, "member");

            var finalExpression = expression != null && expression.Type == ODataExpressionType.Unknown
                ? ConvertFromUnknownType(expression, typeof(ODataEntity))
                : expression;

			return new ODataMemberAccessExpression(finalExpression, member);
		}

        internal static ODataSortKeyExpression SortKey(ODataExpression expression, bool descending = false)
		{
			Throw.IfNull(expression, "expression");
			
			return new ODataSortKeyExpression(expression, descending: descending);
		}

        internal static ODataSelectColumnExpression SelectStar()
		{
			return SelectColumn(null, allColumns: true);
		}

        internal static ODataSelectColumnExpression SelectColumn(ODataMemberAccessExpression memberAccess, bool allColumns)
		{
			Throw.If(
                allColumns && memberAccess != null && memberAccess.Type != ODataExpressionType.Complex && memberAccess.Type != ODataExpressionType.Unknown, 
                "'*' can only be selected for non-primitive types!"
            );
			Throw.If(memberAccess == null && !allColumns, "If no property path is specified, then '*' must be selected!");

            var finalMemberAccess = allColumns && memberAccess != null && memberAccess.Type == ODataExpressionType.Unknown
                ? ConvertFromUnknownType(memberAccess, typeof(object))
                : memberAccess;
			return new ODataSelectColumnExpression(finalMemberAccess, allColumns: allColumns);
		}

        internal static ODataQueryExpression Query(
			ODataExpression filter = null,
			IReadOnlyList<ODataSortKeyExpression> orderBy = null,
			int? top = null,
			int skip = 0,
			string format = null,
			ODataInlineCountOption inlineCount = ODataInlineCountOption.None,
			IReadOnlyList<ODataSelectColumnExpression> select = null)
		{
			Throw.If(filter != null && filter.Type != ODataExpressionType.Boolean && filter.Type != ODataExpressionType.Unknown, "filter: must be boolean-typed");
            var finalFilter = filter != null && filter.Type == ODataExpressionType.Unknown
                ? ConvertFromUnknownType(filter, typeof(bool))
                : filter;

			if (top.HasValue)
			{
				Throw.IfOutOfRange(top.Value, min: 0, paramName: "top");
			}
			Throw.IfOutOfRange(skip, min: 0, paramName: "skip");

			if (format != null)
			{
				Throw.If(string.IsNullOrWhiteSpace(format), "format: must not be empty or whitespace");
			}

			return new ODataQueryExpression(
				filter: finalFilter,
				orderBy: orderBy != null ? orderBy.ToImmutableList() : Empty<ODataSortKeyExpression>.Array,
				top: top,
				skip: skip,
				format: format,
				inlineCount: inlineCount,
				select: select != null ? select.ToImmutableList() : Empty<ODataSelectColumnExpression>.Array
			);
		}

        #region ---- Helpers ----
        private static TExpression ConvertFromUnknownType<TExpression>(TExpression expression, Type type)
            where TExpression : ODataExpression
        {
            // sanity check
            Throw.If(expression.Type != ODataExpressionType.Unknown, "expression: must be of unknown type");

            switch (expression.Kind)
            {
                case ODataExpressionKind.MemberAccess:
                    var memberAccess = (ODataMemberAccessExpression)expression.As<ODataExpression>();
                    return (TExpression)MemberAccess(memberAccess.Expression, ODataEntity.GetProperty(memberAccess.Member.Name, type)).As<ODataExpression>();
                default:
                    throw Throw.UnexpectedCase(expression.Kind);
            }
        }

        private static TExpression LiftNullToUnknown<TExpression>(TExpression expression)
            where TExpression : ODataExpression
        {
            // sanity check
            Throw.If(expression.Type != ODataExpressionType.Null, "expression: must be of null type");

            switch (expression.Kind) 
            {
                case ODataExpressionKind.Constant:
                    return (TExpression)Constant(((ODataConstantExpression)expression.As<ODataExpression>()).Value, typeof(ODataObject)).As<ODataExpression>();
                default:
                    throw Throw.UnexpectedCase(expression.Kind);
            }
        }

        private static Type GetCanonicalArgumentType(ODataBinaryOp @operator) 
        {
            switch (@operator) 
            {
                case ODataBinaryOp.Add:
                case ODataBinaryOp.Subtract:
                case ODataBinaryOp.Multiply:
                case ODataBinaryOp.Divide:
                case ODataBinaryOp.Modulo:
                    // the reason for this is that int can be cast implicitly to most of the other primitive types,
                    // so an expression like substring('foo', A add B) will work if A and B are integers
                    // alternatively, we could return Unknown for these
                    return typeof(int?);
                case ODataBinaryOp.And:
                case ODataBinaryOp.Or:
                    return typeof(bool);
                default:
                    return typeof(ODataObject);
            }
        }
        #endregion
        #endregion
    }
}
