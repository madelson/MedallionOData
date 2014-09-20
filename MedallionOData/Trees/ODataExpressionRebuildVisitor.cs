using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
    internal abstract class ODataExpressionRebuildVisitor : ODataExpressionVisitor
    {
        private readonly Stack<ODataExpression> results = new Stack<ODataExpression>();

        protected ODataExpression PopResult() { return this.results.Pop(); }
        protected void Return(ODataExpression expression) { this.results.Push(expression); }

        protected override void Visit(ODataExpression node)
        {
            if (node == null)
            {
                this.Return(null);
            }
            else
            {
                base.Visit(node);
            }
        }

        protected override void VisitBinaryOp(ODataBinaryOpExpression node)
        {
            base.VisitBinaryOp(node);
            var newRight = this.results.Pop();
            var newLeft = this.results.Pop();
            if (newLeft != node.Left || newRight != node.Right)
            {
                this.Return(ODataBinaryOpExpression.BinaryOp(newLeft, node.Operator, newRight));
            }
            else
            {
                this.Return(node);
            }
        }

        protected override void VisitCall(ODataCallExpression node)
        {
            var resultList = this.RebuildList(node.Arguments);
            this.Return(
                resultList == node.Arguments
                    ? node
                    : ODataExpression.Call(node.Function, node.Arguments)
            );
        }

        protected override void VisitConstant(ODataConstantExpression node)
        {
            base.VisitConstant(node);
            this.Return(node);
        }

        protected override void VisitConvert(ODataConvertExpression node)
        {
            base.VisitConvert(node);
            var result = this.PopResult();
            this.Return(result == node.Expression ? node : ODataExpression.Convert(result, node.ClrType));
        }

        protected override void VisitMemberAccess(ODataMemberAccessExpression node)
        {
            base.VisitMemberAccess(node);
            var result = this.PopResult();
            this.Return(
                result == node.Expression
                    ? node
                    : ODataMemberAccessExpression.MemberAccess((ODataMemberAccessExpression)result, node.Member)
            );
        }

        protected override void VisitQuery(ODataQueryExpression node)
        {
            this.Visit(node.Filter);
            var newFilter = this.PopResult();
            var newOrderBy = this.RebuildList(node.OrderBy);
            var newSelect = this.RebuildList(node.Select);
            this.Return(
                newFilter == node.Filter && newOrderBy == node.OrderBy && newSelect == node.Select
                    ? node
                    : node.Update(filter: newFilter, orderBy: newOrderBy, select: newSelect)
            );
        }

        protected override void VisitSelectColumn(ODataSelectColumnExpression node)
        {
            base.VisitSelectColumn(node);
            var result = this.PopResult();
            this.Return(result == node.Expression ? node : ODataExpression.SelectColumn((ODataMemberAccessExpression)result, node.AllColumns));
        }

        protected override void VisitSortKey(ODataSortKeyExpression node)
        {
            base.VisitSortKey(node);
            var result = this.PopResult();
            this.Return(result == node.Expression ? node : ODataExpression.SortKey(result, node.Descending));
        }

        protected override void VisitUnaryOp(ODataUnaryOpExpression node)
        {
            base.VisitUnaryOp(node);
            var result = this.PopResult();
            this.Return(result == node.Operand ? node : ODataUnaryOpExpression.UnaryOp(node.Operand, node.Operator));
        }

        private IReadOnlyList<TExpression> RebuildList<TExpression>(IReadOnlyList<TExpression> list)
            where TExpression : ODataExpression
        {
            TExpression[] rebuilt = null;
            for (var i = 0; i < list.Count; ++i)
            {
                this.Visit(list[i]);
                var result = this.PopResult();

                if (rebuilt != null) 
                {
                    rebuilt[i] = (TExpression)result;
                }
                else if (result != list[i])
                {
                    rebuilt = new TExpression[list.Count];
                    for (var j = 0; j < i; ++j)
                    {
                        rebuilt[j] = list[j];
                    }
                    rebuilt[i] = (TExpression)result;
                }
            }

            return rebuilt ?? list;
        }
    }
}
