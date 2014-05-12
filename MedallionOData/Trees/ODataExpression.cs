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
		protected ODataExpression(ODataExpressionKind kind, ODataExpressionType type, Type clrType)
		{
            Throw.IfNull(clrType, "clrType");
            Throw.If(!type.IsCompatibleWith(clrType), "clrType must be compatible with kind");

			this.Kind = kind;
			this.Type = type;
            this.ClrType = clrType;
		}

		public ODataExpressionKind Kind { get; private set; }
		public ODataExpressionType Type { get; private set; }
        public Type ClrType { get; private set; }

		#region ---- Static factories ----
        private static readonly IReadOnlyCollection<ODataExpressionType> NonNumericComparableTypes = new[] { ODataExpressionType.DateTime, ODataExpressionType.DateTimeOffset, ODataExpressionType.Time };
		public static ODataBinaryOpExpression BinaryOp(ODataExpression left, ODataBinaryOp @operator, ODataExpression right)
		{
			Throw.IfNull(left, "left");
			Throw.IfNull(right, "right");

            // add in implicit conversions as needed
            ODataExpression convertedLeft, convertedRight;
			if (left.Type == right.Type)
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
                throw new ArgumentException(string.Format("Types {0} and {1} cannot be used with operator {2}", left.Type, right.Type, @operator));
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
                    Throw.If(!operandType.IsNumeric() && !NonNumericComparableTypes.Contains(operandType), "Operator requires a type with comparison operators defined");
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

		public static ODataUnaryOpExpression UnaryOp(ODataExpression operand, ODataUnaryOp @operator)
		{
			Throw.IfNull(operand, "operand");
			Throw.If(operand.Type != ODataExpressionType.Boolean, "operand: must be a boolean type");

			return new ODataUnaryOpExpression(operand, @operator);
		}

		public static ODataConvertExpression Convert(ODataExpression expression, Type clrType)
		{
			Throw.IfNull(expression, "expression");
            Throw.IfNull(clrType, "clrType");
            
            Throw<ArgumentException>.If(
                // can only convert where (1) a cast is valid or (2) a cast might succeed because the target type
                // derives from of the expression type
                !expression.ClrType.IsCastableTo(clrType)
                    && !clrType.IsAssignableFrom(expression.ClrType),
                () => "cannot convert from " + expression.ClrType + " to " + clrType
            );
			return new ODataConvertExpression(expression, clrType);
		}

		public static ODataConstantExpression Constant(object value, Type clrType = null)
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

		public static ODataCallExpression Call(ODataFunction function, IEnumerable<ODataExpression> arguments)
		{
			var argumentsList = (arguments as IReadOnlyList<ODataExpression>) ?? arguments.ToArray();
			var signatures = FunctionSignatures[function];
			var matches =
				(from s in signatures
                 let args = s.Arguments.Select((v, i) => new { Value = v, Index = i })
			    let score = s.Arguments.Count != argumentsList.Count ? default(int?)
				    : args.All(iv => !iv.Value.HasValue || iv.Value.Value == argumentsList[iv.Index].Type) ? 0
				    : args.All(iv => !iv.Value.HasValue || argumentsList[iv.Index].Type.IsImplicityCastableTo(iv.Value.Value)) ? 1
				    : default(int?)
				where score.HasValue
				orderby score
				select new { sig = s, score })
				.ToArray();
			Throw<ArgumentException>.If(matches.Length == 0, () => string.Format("No version of {0} matched the argument types [{1}]", function, arguments.Select(a => a.Type).ToDelimitedString(", ")));
			Throw<ArgumentException>.If(matches.Length > 1 && matches[0].score == matches[1].score, () => string.Format("Ambiguous match for argument types [{0}] between {1} and {2}", arguments.Select(a => a.Type).ToDelimitedString(", "), matches[0].sig, matches[1].sig));

			var match = matches[0].sig;
			var castArguments = argumentsList.Select((a, i) => (!match.Arguments[i].HasValue || a.Type == match.Arguments[i]) 
                    ? a 
					: Convert(a, match.Arguments[i].Value.ToClrType()))
				.ToList()
				.AsReadOnly();

            // the ?? here on return type handles cast, whose return type is dynamic depending on its arguments
			return new ODataCallExpression(
                function, 
                castArguments, 
                match.ReturnType.HasValue
                    ? match.ReturnType.Value.ToClrType() 
                    : (Type)((ODataConstantExpression)castArguments[1]).Value
            );
		}

		public static ODataMemberAccessExpression MemberAccess(ODataMemberAccessExpression expression, PropertyInfo member)
		{
			Throw.IfNull(member, "member");

			return new ODataMemberAccessExpression(expression, member);
		}

		public static ODataSortKeyExpression SortKey(ODataExpression expression, ODataSortDirection direction = ODataSortDirection.Ascending)
		{
			Throw.IfNull(expression, "expression");
			
			return new ODataSortKeyExpression(expression, direction);
		}

		public static ODataSelectColumnExpression SelectStar()
		{
			return SelectColumn(null, allColumns: true);
		}

		public static ODataSelectColumnExpression SelectColumn(ODataMemberAccessExpression memberAccess, bool allColumns)
		{
			Throw.If(allColumns && memberAccess != null && memberAccess.Type != ODataExpressionType.Complex, "'*' can only be selected for non-primitive types!");
			Throw.If(memberAccess == null && !allColumns, "If no property path is specified, then '*' must be selected!");

			return new ODataSelectColumnExpression(memberAccess, allColumns: allColumns);
		}

		public static ODataQueryExpression Query(
			ODataExpression filter = null,
			IReadOnlyList<ODataSortKeyExpression> orderBy = null,
			int? top = null,
			int skip = 0,
			string format = null,
			ODataInlineCountOption inlineCount = ODataInlineCountOption.None,
			IReadOnlyList<ODataSelectColumnExpression> select = null)
		{
			Throw.If(filter != null && filter.Type != ODataExpressionType.Boolean, "filter: must be boolean-typed");
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
				filter: filter,
				orderBy: orderBy != null ? orderBy.ToImmutableList() : Empty<ODataSortKeyExpression>.Array,
				top: top,
				skip: skip,
				format: format,
				inlineCount: inlineCount,
				select: select != null ? select.ToImmutableList() : Empty<ODataSelectColumnExpression>.Array
			);
		}
		#endregion
	}
}
