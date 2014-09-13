using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    // TODO BIT MODE
    // TODO OFFSET/FETCH w/out sort

    internal sealed class ODataToSqlTranslator : ODataExpressionVisitor
    {
        private const string Alias = "q";

        private readonly StringBuilder sqlBuilder = new StringBuilder();
        private readonly DatabaseProvider databaseProvider;
        private readonly string tableSql;

        private ODataToSqlTranslator(DatabaseProvider databaseProvider, string tableSql) 
        {
            this.databaseProvider = databaseProvider;
            this.tableSql = tableSql;
        }

        public static string Translate(DatabaseProvider databaseProvider, string tableSql, ODataQueryExpression query, out List<Parameter> parameters)
        {
            Throw.IfNull(databaseProvider, "databaseProvider");
            Throw.If(string.IsNullOrEmpty(tableSql), "tableSql is required");
            Throw.IfNull(query, "query");
            
            var translator = new ODataToSqlTranslator(databaseProvider, tableSql);
            translator.Visit(query);

            var sql = translator.sqlBuilder.ToString();
            parameters = translator.parameters;
            return sql;
        }

        private static readonly IReadOnlyList<ODataBinaryOp> BoolOps = new[] { ODataBinaryOp.And, ODataBinaryOp.Or };
        protected override void VisitBinaryOp(ODataBinaryOpExpression node)
        {
            if (node.Operator == ODataBinaryOp.Modulo) 
            {
                this.databaseProvider.RenderModuloOperator(s => this.Write(s), () => this.Write(node.Left), () => this.Write(node.Right));
            }
            else 
            {
                this.Write("(").Write(node.Left).Write(" ")
                    .Write(GetBinOpString(node.Operator))
                    .Write(" ").Write(node.Right).Write(")");
            }
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
                    this.Write(" AS ").Write(this.databaseProvider.GetSqlTypeName(toType)).Write(")");
                    break;
                case ODataFunction.Ceiling:
                    this.VisitCallHelper(this.databaseProvider.UseAbbreviatedCeilingFunction ? "CEIL" : "CEILING", node);
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
                    this.databaseProvider.RenderDatePartFunctionCall(node.Function, s => this.Write(s), () => this.Write(node.Arguments[0]));
                    break;
                case ODataFunction.IndexOf:
                    this.databaseProvider.RenderIndexOfFunctionCall(s => this.Write(s), renderNeedleArgument: () => this.Write(node.Arguments[1]), renderHaystackArgument: () => this.Write(node.Arguments[0]));
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
                    this.Write(endsWithExpression);
                    break;
                case ODataFunction.StartsWith:
                    // startswith => INDEXOF(needle, haystack) = 0
                    var startsWithExpression = ODataExpression.BinaryOp(
                        ODataExpression.Call(ODataFunction.IndexOf, node.Arguments),
                        ODataBinaryOp.Equal,
                        ODataExpression.Constant(0)
                    );
                    this.Write(startsWithExpression);
                    break;
                case ODataFunction.SubstringOf:
                    // substringof => INDEXOF(needle, haystack) >= 0
                    var substringOfExpression = ODataExpression.BinaryOp(
                        ODataExpression.Call(ODataFunction.IndexOf, node.Arguments.Reverse()),
                        ODataBinaryOp.GreaterThanOrEqual,
                        ODataExpression.Constant(0)
                    );
                    this.Write(substringOfExpression);
                    break;
                case ODataFunction.Floor:
                    this.VisitCallHelper("FLOOR", node);
                    break;
                case ODataFunction.Length:
                    this.VisitCallHelper(this.databaseProvider.StringLengthFunctionName, node);
                    break;
                case ODataFunction.Replace:
                    this.VisitCallHelper("REPLACE", node);
                    break;
                case ODataFunction.Round:
                    this.databaseProvider.RenderRoundFunctionCall(s => this.Write(s), () => this.Write(node.Arguments[0]));
                    break;
                case ODataFunction.Substring:
                    this.databaseProvider.RenderSubstringFunctionCall(s => this.Write(s), () => this.Write(node.Arguments[0]), () => this.Write(node.Arguments[1]), node.Arguments.Count > 2 ? new Action(() => this.Write(node.Arguments[2])) : null);
                    break;
                case ODataFunction.ToLower:
                    this.VisitCallHelper("LOWER", node);
                    break;
                case ODataFunction.ToUpper:
                    this.VisitCallHelper("UPPER", node);
                    break;
                case ODataFunction.Trim:
                    if (this.databaseProvider.HasTwoSidedTrim)
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
            this.databaseProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(node.ClrType, node.Value));
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
            Throw<NotSupportedException>.If(node.Expression != null, () => "Only primitive properties are supported. Found " + node);
            this.Write(Alias).Write(".");
            this.databaseProvider.RenderColumnName(s => this.Write(s), node.Member);
        }

        protected override void VisitQuery(ODataQueryExpression node)
        {
            const string RowNumberColumnName = "__medallionODataRowNumber";

            var hasPagination = (node.Skip > 0 || node.Top.HasValue);
            var hasRowNumberPagination = hasPagination
                && this.databaseProvider.Pagination == DatabaseProvider.PaginationSyntax.RowNumber;

            if (hasRowNumberPagination)
            {
                this.WriteLine("SELECT *")
                    .WriteLine("FROM (");
            }

            // select
            this.Write("SELECT ");
            if (node.Select.Count > 0)
            {
                for (var i = 0; i < node.Select.Count; ++i)
                {
                    this.Write(", ", @if: i > 0).Write(node.Select[i]);
                }
            }
            else
            {
                this.Write("*");
            }
            if (hasRowNumberPagination)
            {
                this.Write(", ROW_NUMBER() OVER (ORDER BY ");
                if (node.OrderBy.Count > 0)
                {
                    for (var i = 0; i < node.OrderBy.Count; ++i)
                    {
                        this.Write(", ", @if: i > 0).Write(node.OrderBy[i]);
                    }
                }
                else
                {
                    this.Write("RAND()");
                }
                this.Write(") AS ").Write(RowNumberColumnName);
            }
            this.WriteLine();

            // from
            this.Write("FROM ").Write(this.tableSql).Write(" ").Write(Alias).WriteLine();

            // where
            if (node.Filter != null)
            {
                this.Write("WHERE ").Write(node.Filter).WriteLine();
            }

            if (hasRowNumberPagination)
            {
                this.Write(") ").WriteLine(Alias); // close the subquery
                this.Write("WHERE ");
                this.databaseProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(typeof(int), node.Skip));
                this.Write(" < ").Write(Alias).Write(".").Write(RowNumberColumnName);
                if (node.Top.HasValue) 
                {
                    this.Write(" AND ").Write(Alias).Write(".").Write(RowNumberColumnName).Write(" <= ");
                    this.databaseProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(typeof(int), node.Skip + node.Top.Value));
                }
                this.WriteLine();
            }

            // order by
            if (node.OrderBy.Count > 0)
            {
                this.Write("ORDER BY ");
                for (var i = 0; i < node.OrderBy.Count; ++i)
                {
                    this.Write(", ", @if: i > 0).Write(node.OrderBy[i]);
                }
                this.WriteLine();
            }

            // skip/take
            if (hasPagination)
            {
                switch (this.databaseProvider.Pagination)
                {
                    case DatabaseProvider.PaginationSyntax.OffsetFetch:
                        this.Write("OFFSET ");
                        this.databaseProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(typeof(int), node.Skip));
                        this.WriteLine(" ROWS");
                        if (node.Top.HasValue)
                        {
                            this.Write("FETCH NEXT ");
                            this.databaseProvider.RenderParameterReference(s => this.Write(s), this.CreateParameter(typeof(int), node.Top.Value));
                            this.WriteLine(" ROWS ONLY");
                        }
                        break;
                    case DatabaseProvider.PaginationSyntax.Limit:
                        this.Write("LIMIT ").Write(node.Skip).Write(", ")
                            .WriteLine(node.Top ?? "18446744073709551615".As<object>());
                        break;
                    default:
                        throw Throw.UnexpectedCase(this.databaseProvider.Pagination);
                }
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
                    this.Write("(NOT ").Write(node.Operand).Write(")");
                    break;
                default:
                    throw Throw.UnexpectedCase(node.Operator);
            }
        }

        #region ---- Helpers ----
        private ODataToSqlTranslator Write(object obj, bool @if = true)
        {
            if (@if)
            {
                var expression = obj as ODataExpression;
                if (expression != null)
                {
                    this.Visit(expression);
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

        private readonly Stack<bool> _bitMode = new Stack<bool>();
        private ODataToSqlTranslator PushBitMode(bool value)
        {
            this._bitMode.Push(value);
            return this;
        }

        private ODataToSqlTranslator PopBitMode()
        {
            this._bitMode.Pop();
            return this;
        }

        private bool BitMode { get { return this._bitMode.Peek(); } }

        private readonly List<Parameter> parameters = new List<Parameter>();
        private Parameter CreateParameter(Type type, object value)
        {
            var parameter = new Parameter("p" + this.parameters.Count, type, value);
            this.parameters.Add(parameter);
            return parameter;
        }
        #endregion
    }
}
