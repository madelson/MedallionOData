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
		protected ODataExpression(ODataExpressionKind kind, ODataExpressionType type)
		{
			this.Kind = kind;
			this.Type = type;
		}

		public ODataExpressionKind Kind { get; private set; }
		public ODataExpressionType Type { get; private set; }

		#region ---- Static factories ----
		public static ODataBinaryOpExpression BinaryOp(ODataExpression left, ODataBinaryOp @operator, ODataExpression right)
		{
			Throw.IfNull(left, "left");
			Throw.IfNull(right, "right");

			if (left.Type == right.Type)
			{
				return new ODataBinaryOpExpression(left, @operator, right);
			}
			if (left.Type.IsImplicityCastableTo(right.Type))
			{
				return new ODataBinaryOpExpression(Convert(left, right.Type), @operator, right);
			}
			if (right.Type.IsImplicityCastableTo(left.Type))
			{
				return new ODataBinaryOpExpression(left, @operator, Convert(right, left.Type));
			}

			throw new ArgumentException(string.Format("Types {0} and {1} are incompatible", left.Type, right.Type));
		}

		public static ODataUnaryOpExpression UnaryOp(ODataExpression operand, ODataUnaryOp @operator)
		{
			Throw.IfNull(operand, "operand");
			Throw.If(operand.Type != ODataExpressionType.Boolean, "operand: must be a boolean type");

			return new ODataUnaryOpExpression(operand, @operator);
		}

		public static ODataConvertExpression Convert(ODataExpression expression, ODataExpressionType type)
		{
			Throw.IfNull(expression, "expression");

			return new ODataConvertExpression(expression, type);
		}

		public static ODataConstantExpression Constant(object value, ODataExpressionType? type = null)
		{
			ODataExpressionType finalType;
			if (type.HasValue)
			{
				Throw.If(value != null && type.Value == ODataExpressionType.Null, "type: the null type can only accompany a null value");
				Throw<ArgumentException>.If(value != null && value.GetType().ToODataExpressionType() != type, () => string.Format("value of type {0} does not fit OData type {1}", value.GetType(), type));
				finalType = type.Value;
			}
			else
			{
				finalType = value == null ? ODataExpressionType.Null : value.GetType().ToODataExpressionType();
			}
			Throw<ArgumentException>.If(!finalType.IsPrimitive() && !finalType.EqualsAny(ODataExpressionType.Null, ODataExpressionType.Type), () => "a constant must be of a primitive type, null, or the Type type. Found " + finalType);

			return new ODataConstantExpression(value, finalType);
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
			var castArguments = argumentsList.Select((a, i) => (!match.Arguments[i].HasValue || a.Type == match.Arguments[i]) ? a 
					: Convert(a, match.Arguments[i].Value))
				.ToList()
				.AsReadOnly();

			return new ODataCallExpression(function, castArguments, match.ReturnType ?? ((Type)((ODataConstantExpression)castArguments[1]).Value).ToODataExpressionType());
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
