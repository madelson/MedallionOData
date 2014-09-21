using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    // TODO
    // ODataValue

    internal sealed class ODataToSqlTranslator : ODataExpressionVisitor
    {
        private const string Alias = "q";

        private readonly StringBuilder sqlBuilder = new StringBuilder();
        private readonly SqlSyntax syntaxProvider;
        private readonly string tableSql;

        private ODataToSqlTranslator(SqlSyntax syntaxProvider, string tableSql) 
        {
            this.syntaxProvider = syntaxProvider;
            this.tableSql = tableSql;
        }

        public static string Translate(SqlSyntax syntaxProvider, string tableSql, ODataQueryExpression query, out List<Parameter> parameters)
        {
            Throw.IfNull(syntaxProvider, "databaseProvider");
            Throw.If(string.IsNullOrEmpty(tableSql), "tableSql is required");
            Throw.IfNull(query, "query");

            var translator = new ODataToSqlTranslator(syntaxProvider, tableSql);
            translator.Visit(query);

            var sql = translator.sqlBuilder.ToString();
            parameters = translator.parameters.Values.ToList();
            return sql;
        }

        private static readonly IReadOnlyList<ODataBinaryOp> BoolOps = new[] { ODataBinaryOp.And, ODataBinaryOp.Or };
        protected override void VisitBinaryOp(ODataBinaryOpExpression node)
        {
            this.Write("(");
            switch (node.Operator) 
            {
                case ODataBinaryOp.Equal:
                case ODataBinaryOp.NotEqual:
                    // null-safe ==: (a is not null and b is not null and a = b) or (a is null and b is null)
                    // null-safe !=: (a is null or b is null or a <> b) and (a is not null or b is not null)
                    var @is = node.Operator == ODataBinaryOp.Equal ? " IS " : " IS NOT ";
                    var isNot = node.Operator == ODataBinaryOp.Equal ? " IS NOT " : " IS ";
                    var and = node.Operator == ODataBinaryOp.Equal ? " AND " : " OR ";
                    var or = node.Operator == ODataBinaryOp.Equal ? " OR " : " AND ";
                    this.Write("(")
                        .Write(node.Left).Write(isNot).Write("NULL")
                        .Write(and).Write(node.Right).Write(isNot).Write("NULL")
                        .Write(and).Write(node.Left).Write(GetBinOpString(node.Operator)).Write(node.Right)
                        .Write(")")
                        .Write(or)
                        .Write("(")
                        .Write(node.Left).Write(@is).Write("NULL")
                        .Write(and).Write(node.Right).Write(@is).Write("NULL")
                        .Write(")");
                    break;
                case ODataBinaryOp.Modulo:
                    this.syntaxProvider.RenderModuloOperator(s => this.Write(s), () => this.Write(node.Left), () => this.Write(node.Right));
                    break;
                default:
                    var argBoolMode = BoolOps.Contains(node.Operator) ? BoolMode.Bool : BoolMode.Bit;
                    this.Write(node.Left, boolMode: argBoolMode).Write(" ").Write(GetBinOpString(node.Operator)).Write(" ").Write(node.Right, boolMode: argBoolMode);
                    break;
            }
            this.Write(")");
        }

        private static string GetBinOpString(ODataBinaryOp op)
        {
            switch (op) 
            {
                case ODataBinaryOp.Add: return "+";
                case ODataBinaryOp.And: return "AND";
                case ODataBinaryOp.Divide: return "/";
                case ODataBinaryOp.Equal: return "=";
                case ODataBinaryOp.GreaterThan: return ">";
                case ODataBinaryOp.GreaterThanOrEqual: return ">=";
                case ODataBinaryOp.LessThan: return "<";
                case ODataBinaryOp.LessThanOrEqual: return "<=";
                case ODataBinaryOp.Modulo: throw new InvalidOperationException("Modulo is handled separately!");
                case ODataBinaryOp.Multiply: return "*";
                // ansii standard, see http://blog.sqlauthority.com/2013/07/08/sql-difference-between-and-operator-used-for-not-equal-to-operation/
                case ODataBinaryOp.NotEqual: return "<>";
                case ODataBinaryOp.Or: return "OR";
                case ODataBinaryOp.Subtract: return "-";
                default: throw Throw.UnexpectedCase(op);
            }
        }

        protected override void VisitCall(ODataCallExpression node)
        {
            // reference for different function syntax http://users.atw.hu/sqlnut/sqlnut2-chp-4-sect-4.html 
            switch (node.Function) 
            {
                case ODataFunction.Cast:
                    this.Write("CAST(").Write(node.Arguments[0]);
                    var toType = ((Type)((ODataConstantExpression)node.Arguments[1]).Value).ToODataExpressionType();
                    this.Write(" AS ").Write(this.syntaxProvider.GetSqlTypeName(toType)).Write(")");
                    break;
                case ODataFunction.Ceiling:
                    this.VisitCallHelper(this.syntaxProvider.UseAbbreviatedCeilingFunction ? "CEIL" : "CEILING", node);
                    break;
                case ODataFunction.Concat:
                    this.Write("(").Write(node.Arguments[0])
                        .Write(" + ")
                        .Write(node.Arguments[1]).Write(")");
                    break;
                case ODataFunction.Second:
                case ODataFunction.Minute:
                case ODataFunction.Hour:
                case ODataFunction.Day:
                case ODataFunction.Month:
                case ODataFunction.Year:
                    this.syntaxProvider.RenderDatePartFunctionCall(node.Function, s => this.Write(s), () => this.Write(node.Arguments[0]));
                    break;
                case ODataFunction.EndsWith:
                    // endswith => (INDEXOF(needle, haystack) = LEN(haystack) - LEN(needle)) OR LEN(needle) = 0
                    var needleLengthExpression = ODataExpression.Call(ODataFunction.Length, new[] { node.Arguments[1] });
                    var endsWithExpression = ODataExpression.BinaryOp(
                        ODataExpression.BinaryOp(
                            ODataExpression.Call(ODataFunction.IndexOf, node.Arguments),
                            ODataBinaryOp.Equal,
                            ODataExpression.BinaryOp(
                                ODataExpression.Call(ODataFunction.Length, new[] { node.Arguments[0] }),
                                ODataBinaryOp.Subtract,
                                needleLengthExpression
                            )
                        ),
                        ODataBinaryOp.Or,
                        ODataExpression.BinaryOp(
                            needleLengthExpression,
                            ODataBinaryOp.Equal,
                            ODataExpression.Constant(0)
                        )
                    );
                    this.Visit(endsWithExpression);
                    break;
                case ODataFunction.StartsWith:
                    // startswith => INDEXOF(needle, haystack) = 0
                    var startsWithExpression = ODataExpression.BinaryOp(
                        ODataExpression.Call(ODataFunction.IndexOf, node.Arguments),
                        ODataBinaryOp.Equal,
                        ODataExpression.Constant(0)
                    );
                    this.Visit(startsWithExpression);
                    break;
                case ODataFunction.SubstringOf:
                    // substringof => INDEXOF(needle, haystack) >= 0
                    var substringOfExpression = ODataExpression.BinaryOp(
                        ODataExpression.Call(ODataFunction.IndexOf, node.Arguments.Reverse()),
                        ODataBinaryOp.GreaterThanOrEqual,
                        ODataExpression.Constant(0)
                    );
                    this.Visit(substringOfExpression);
                    break;
                case ODataFunction.IndexOf:
                    this.syntaxProvider.RenderIndexOfFunctionCall(s => this.Write(s), renderNeedleArgument: () => this.Write(node.Arguments[1]), renderHaystackArgument: () => this.Write(node.Arguments[0]));
                    break;
                case ODataFunction.Floor:
                    this.VisitCallHelper("FLOOR", node);
                    break;
                case ODataFunction.Length:
                    this.VisitCallHelper(this.syntaxProvider.StringLengthFunctionName, node);
                    break;
                case ODataFunction.Replace:
                    this.VisitCallHelper("REPLACE", node);
                    break;
                case ODataFunction.Round:
                    this.syntaxProvider.RenderRoundFunctionCall(s => this.Write(s), () => this.Write(node.Arguments[0]));
                    break;
                case ODataFunction.Substring:
                    this.syntaxProvider.RenderSubstringFunctionCall(s => this.Write(s), () => this.Write(node.Arguments[0]), () => this.Write(node.Arguments[1]), node.Arguments.Count > 2 ? new Action(() => this.Write(node.Arguments[2])) : null);
                    break;
                case ODataFunction.ToLower:
                    this.VisitCallHelper("LOWER", node);
                    break;
                case ODataFunction.ToUpper:
                    this.VisitCallHelper("UPPER", node);
                    break;
                case ODataFunction.Trim:
                    if (this.syntaxProvider.HasTwoSidedTrim)
                    {
                        this.VisitCallHelper("TRIM", node);
                    }
                    else
                    {
                        // call both LTRIM and RTRIM
                        this.VisitCallHelper("LTRIM(RTRIM", node);
                        this.Write(")");
                    }
                    break;
                case ODataFunction.IsOf:
                    throw new NotSupportedException(node.Function.ToString());
            }
        }

        private void VisitCallHelper(string functionName, ODataCallExpression node)
        {
            this.Write(functionName).Write("(");
            for (var i = 0; i < node.Arguments.Count; ++i)
            {
                this.Write(", ", @if: i > 0);
                this.Visit(node.Arguments[i]);
            }
            this.Write(")");
        }

        protected override void VisitConstant(ODataConstantExpression node)
        {
            this.syntaxProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(node));
        }

        protected override void VisitConvert(ODataConvertExpression node)
        {
            var convertExpression = node.Expression.Type.IsImplicityCastableTo(node.Type)
                ? node.Expression
                : ODataExpression.Call(ODataFunction.Cast, new[] { node.Expression, ODataExpression.Constant(node.ClrType) });
            this.Visit(convertExpression);
        }

        protected override void VisitMemberAccess(ODataMemberAccessExpression node)
        {
            // MA: we can't check for node.Type != Complex here because if we are dynamic then we can get confused with
            // statements like x => x.Foo != null
            Throw<NotSupportedException>.If(node.Expression != null, () => "Only primitive properties are supported. Found " + node);
            this.Write(Alias).Write(".");
            this.syntaxProvider.RenderColumnName(s => this.Write(s), node.Member);
        }

        protected override void VisitQuery(ODataQueryExpression node)
        {
            const string RowNumberColumnName = "__medallionODataRowNumber";

            var isCounting = node.InlineCount == ODataInlineCountOption.AllPages;

            // we never do pagination when doing counting. This is because, in OData, count ignores pagination
            var hasPagination = !isCounting && (node.Skip > 0 || node.Top.HasValue);

            // we special-case "top 0" and just do WHERE 1=0 instead. This is because offset-fetch 
            // does not allow a fetch of 0
            if (hasPagination && (node.Top ?? 1) == 0)
            {
                var emptyQuery = node.Update(
                    filter: ODataExpression.Constant(false),
                    top: null
                );
                this.Visit(emptyQuery);
                return;
            }

            if (isCounting)
            {
                this.WriteLine("SELECT COUNT(1) AS theCount")
                    .WriteLine("FROM (");
            }

            var hasRowNumberPagination = hasPagination
                && this.syntaxProvider.Pagination == SqlSyntax.PaginationSyntax.RowNumber;
            if (hasRowNumberPagination)
            {
                this.Write("SELECT ")
                    .WriteCommaDelimitedList(node.Select, ifEmpty: "*")
                    .WriteLine()
                    .WriteLine("FROM (");
            }

            // select
            this.Write("SELECT ");
            if (hasRowNumberPagination)
            {
                this.Write("* , ROW_NUMBER() OVER (ORDER BY ")
                    .WriteCommaDelimitedList(node.OrderBy, ifEmpty: "RAND()")
                    .Write(") AS ").Write(RowNumberColumnName);
            }
            else
            {
                this.WriteCommaDelimitedList(node.Select, ifEmpty: "*");
            }
            this.WriteLine();

            // from
            this.Write("FROM ").Write(this.tableSql).Write(" ").Write(Alias).WriteLine();

            // where
            if (node.Filter != null)
            {
                this.Write("WHERE ").Write(node.Filter, boolMode: BoolMode.Bool).WriteLine();
            }

            if (hasRowNumberPagination)
            {
                this.Write(") ").WriteLine(Alias); // close the subquery
                this.Write("WHERE ");
                this.syntaxProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(ODataExpression.Constant(node.Skip)));
                this.Write(" < ").Write(Alias).Write(".").Write(RowNumberColumnName);
                if (node.Top.HasValue) 
                {
                    this.Write(" AND ").Write(Alias).Write(".").Write(RowNumberColumnName).Write(" <= ");
                    this.syntaxProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(ODataExpression.Constant(node.Skip + node.Top.Value)));
                }
                this.WriteLine();
            }

            // order by
            // we avoid rendering orderby when counting. We don't have to worry about pagination
            // since hasPagination is always false when counting
            if ((node.OrderBy.Count > 0 && !isCounting)
                // when doing offset-fetch pagination, we are required to have an order by clause
                || (hasPagination && this.syntaxProvider.Pagination == SqlSyntax.PaginationSyntax.OffsetFetch))
            {
                this.Write("ORDER BY ")
                    .WriteCommaDelimitedList(node.OrderBy, ifEmpty: "RAND()")
                    .WriteLine();
            }

            // skip/take
            if (hasPagination)
            {
                switch (this.syntaxProvider.Pagination)
                {
                    case SqlSyntax.PaginationSyntax.OffsetFetch:
                        this.Write("OFFSET ");
                        this.syntaxProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(ODataExpression.Constant(node.Skip)));
                        this.WriteLine(" ROWS");
                        if (node.Top.HasValue)
                        {
                            this.Write("FETCH NEXT ");
                            this.syntaxProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(ODataExpression.Constant(node.Top.Value)));
                            this.WriteLine(" ROWS ONLY");
                        }
                        break;
                    case SqlSyntax.PaginationSyntax.Limit:
                        this.Write("LIMIT ").Write(node.Skip).Write(", ")
                            .WriteLine(node.Top ?? "18446744073709551615".As<object>());
                        break;
                    case SqlSyntax.PaginationSyntax.RowNumber:
                        // handled above
                        break;
                    default:
                        throw Throw.UnexpectedCase(this.syntaxProvider.Pagination);
                }
            }

            if (isCounting)
            {
                this.Write(") ").WriteLine(Alias); // close the subquery
            }
        }

        protected override void VisitSelectColumn(ODataSelectColumnExpression node)
        {
            if (node.AllColumns)
            {
                Throw<NotSupportedException>.If(node.Expression != null, () => "Only primitive properties are supported. Found " + node);
                this.Write(Alias).Write(".*");
            }
            else
            {
                this.Write(node.Expression);
            }
        }

        protected override void VisitSortKey(ODataSortKeyExpression node)
        {
            this.Write(node.Expression)
                .Write(" DESC", @if: node.Descending);
        }

        protected override void VisitUnaryOp(ODataUnaryOpExpression node)
        {
            switch (node.Operator)
            {
                case ODataUnaryOp.Not:
                    this.Write("(NOT ").Write(node.Operand, boolMode: BoolMode.Bool).Write(")");
                    break;
                default:
                    throw Throw.UnexpectedCase(node.Operator);
            }
        }

        #region ---- Helpers ----
        private ODataToSqlTranslator Write(object obj, bool @if = true, BoolMode boolMode = BoolMode.Bit)
        {
            if (@if)
            {
                var expression = obj as ODataExpression;
                if (expression != null)
                {
                    var needsBoolModeConversion = !this.syntaxProvider.HasFirstClassBooleanType
                        && boolMode != BoolModeHelper.GetDefaultMode(expression);
                    if (needsBoolModeConversion)
                    {
                        this.Write(boolMode == BoolMode.Bit ? "(CASE WHEN " : "(");
                    }
                    this.Visit(expression);
                    if (needsBoolModeConversion)
                    {
                        // TODO do we want more robust null-comparison logic instead of just "= 1"?
                        this.Write(boolMode == BoolMode.Bit ? " THEN 1 ELSE 0 END)" : " = 1)");
                    }
                }
                else
                {
                    this.sqlBuilder.Append(obj);
                }
            }
            return this;
        }

        private ODataToSqlTranslator WriteLine(object obj = null, bool @if = true)
        {
            if (@if)
            {
                this.Write(obj);
                this.sqlBuilder.AppendLine();
            }
            return this;
        }

        private ODataToSqlTranslator WriteCommaDelimitedList(IReadOnlyList<ODataExpression> list, string ifEmpty)
        {
            if (list.Count > 0)
            {
                for (var i = 0; i < list.Count; ++i)
                {
                    this.Write(", ", @if: i > 0).Write(list[i]);
                }
            }
            else
            {
                this.Write(ifEmpty);
            }

            return this;
        }

        private readonly Dictionary<ODataConstantExpression, Parameter> parameters = new Dictionary<ODataConstantExpression,Parameter>();
        private Parameter CreateParameter(ODataConstantExpression expression)
        {
            Parameter existing;
            if (this.parameters.TryGetValue(expression, out existing))
            {
                return existing;
            }

            var parameter = new Parameter("p" + this.parameters.Count, expression.ClrType, expression.Value);
            this.parameters.Add(expression, parameter);
            return parameter;
        }
        #endregion
    }
}
