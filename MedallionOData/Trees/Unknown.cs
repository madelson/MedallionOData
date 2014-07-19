using Medallion.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
    /// <summary>
    /// A type to be used with <see cref="ODataExpressionType.Unknown"/>
    /// </summary>
    internal sealed class Unknown
    {
        // cannot be constructed
        private Unknown() { }

        // MA: this has some useful rebuild visitor code that I didn't end up needing.
        // leaving it here for now so that I don't have to dig it out of history later
        //#region ---- Translator ----
        //private sealed class ODataUnknownTypeAssigner : ODataExpressionRebuildVisitor
        //{
        //    private readonly Stack<Type> assignedType = new Stack<Type>(capacity: 5);

        //    protected override void Visit(ODataExpression node)
        //    {
        //        if (node.Type == ODataExpressionType.Unknown)
        //        {
        //            base.Visit(node);
        //        }
        //    }

        //    protected override void VisitMemberAccess(ODataMemberAccessExpression node)
        //    {
        //        this.assignedType.Push(typeof(ODataEntity));
        //        var expression = this.VisitAndConsume(node);
        //        this.assignedType.Pop();

        //        this.MarkAsRebuilt(
        //            ODataExpression.MemberAccess((ODataMemberAccessExpression)expression, 
        //            ODataEntity.GetProperty(node.Member.Name, this.assignedType.Peek()))
        //        );
        //    }
        //}

        //// TODO move or remove
        //public abstract class ODataExpressionRebuildVisitor : ODataExpressionVisitor
        //{
        //    private ODataExpression rebuiltExpression;

        //    protected override void VisitBinaryOp(ODataBinaryOpExpression node)
        //    {
        //        var left = this.VisitAndConsume(node.Left);
        //        var right = this.VisitAndConsume(node.Right);
        //        if (left != node.Left || right != node.Right)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.BinaryOp(left, node.Operator, right));
        //        }
        //    }

        //    protected override void VisitCall(ODataCallExpression node)
        //    {
        //        var arguments = this.VisitAndConsumeList(node.Arguments);

        //        if (arguments != node.Arguments)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.Call(node.Function, arguments));
        //        }
        //    }

        //    protected override void VisitConvert(ODataConvertExpression node)
        //    {
        //        var expression = this.VisitAndConsume(node.Expression);
        //        if (expression != node.Expression)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.Convert(expression, node.ClrType));
        //        }
        //    }

        //    protected override void VisitMemberAccess(ODataMemberAccessExpression node)
        //    {
        //        var expression = this.VisitAndConsume(node.Expression);
        //        if (expression != node.Expression)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.MemberAccess((ODataMemberAccessExpression)expression, node.Member));
        //        }
        //    }

        //    protected override void VisitQuery(ODataQueryExpression node)
        //    {
        //        var filter = this.VisitAndConsume(node.Filter);
        //        var orderBy = this.VisitAndConsumeList(node.OrderBy);
        //        var select = this.VisitAndConsumeList(node.Select);

        //        if (filter != node.Filter || orderBy != node.OrderBy || select != node.Select)
        //        {
        //            this.MarkAsRebuilt(node.Update(filter: filter, orderBy: orderBy, select: select));
        //        }
        //    }

        //    protected override void VisitSelectColumn(ODataSelectColumnExpression node)
        //    {
        //        var expression = this.VisitAndConsume(node.Expression);
        //        if (expression != node.Expression)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.SelectColumn((ODataMemberAccessExpression)expression, node.AllColumns));
        //        }
        //    }

        //    protected override void VisitSortKey(ODataSortKeyExpression node)
        //    {
        //        var expression = this.VisitAndConsume(node.Expression);
        //        if (expression != node.Expression)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.SortKey(expression, node.Descending));
        //        }
        //    }

        //    protected override void VisitUnaryOp(ODataUnaryOpExpression node)
        //    {
        //        var operand = this.VisitAndConsume(node.Operand);
        //        if (operand != node.Operand)
        //        {
        //            this.MarkAsRebuilt(ODataExpression.UnaryOp(operand, node.Operator));
        //        }
        //    }

        //    #region ---- Helpers ----
        //    protected void MarkAsRebuilt(ODataExpression node)
        //    {
        //        Throw<InvalidOperationException>.If(this.rebuiltExpression != null, "An expression was rebuilt but not consumed!");
        //        this.rebuiltExpression = node;
        //    }

        //    protected ODataExpression ConsumeRebuiltExpression()
        //    {
        //        var result = this.rebuiltExpression;
        //        this.rebuiltExpression = null;
        //        return result;
        //    }

        //    protected ODataExpression VisitAndConsume(ODataExpression node) 
        //    {
        //        this.Visit(node);
        //        return this.ConsumeRebuiltExpression() ?? node;
        //    }

        //    protected IReadOnlyList<TExpression> VisitAndConsumeList<TExpression>(IReadOnlyList<TExpression> list)
        //        where TExpression : ODataExpression
        //    {
        //        TExpression[] rebuiltArray = null;
        //        for (var i = 0; i < list.Count; ++i)
        //        {
        //            var rebuilt = this.VisitAndConsume(list[i]);
        //            if (rebuilt != list[i])
        //            {
        //                if (rebuiltArray == null)
        //                {
        //                    rebuiltArray = new TExpression[list.Count];
        //                    for (var j = 0; j < i; ++j)
        //                    {
        //                        rebuiltArray[j] = list[j];
        //                    }
        //                }
        //                rebuiltArray[i] = (TExpression)rebuilt;
        //            }
        //            else if (rebuiltArray != null)
        //            {
        //                rebuiltArray[i] = list[i];
        //            }
        //        }

        //        return rebuiltArray ?? list;
        //    }
        //    #endregion
        //}
        //#endregion
    }
}
