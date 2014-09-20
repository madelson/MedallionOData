using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    /// <summary>
    /// Assists in translation to SQL by normalizing away OData constructs that are best expressed in SQL via
    /// other OData constructs
    /// </summary>
    internal sealed class ODataExpressionSqlNormalizer : ODataExpressionRebuildVisitor
    {
        private ODataExpressionSqlNormalizer() { }

        public static ODataQueryExpression Normalize(ODataQueryExpression expression)
        {
            var visitor = new ODataExpressionSqlNormalizer();
            visitor.Visit(expression);
            var normalized = visitor.PopResult();
            return (ODataQueryExpression)normalized;
        }

        protected override void VisitCall(ODataCallExpression node)
        {
            switch (node.Function) 
            {
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
                default:
                    base.VisitCall(node);
                    break;
            }
        }

        protected override void VisitConvert(ODataConvertExpression node)
        {
            var convertExpression = node.Expression.Type.IsImplicityCastableTo(node.Type)
                ? node.Expression
                : ODataExpression.Call(ODataFunction.Cast, new[] { node.Expression, ODataExpression.Constant(node.ClrType) });
            this.Visit(convertExpression);
        }
    }
}
