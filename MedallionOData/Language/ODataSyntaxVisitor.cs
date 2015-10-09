using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    public abstract class ODataSyntaxVisitor
    {
        public virtual ODataSyntaxNode Visit(ODataSyntaxNode node)
        {
            return node?.Accept(this);
        }

        protected internal virtual ODataExpressionSyntax VisitGroup(ODataGroupSyntax node)
        {
            var expression = this.Visit(node.Expression);
            return expression == node.Expression
                ? node
                : EnsureExpression(expression).Group();
        }

        protected internal virtual ODataExpressionSyntax VisitBinary(ODataBinarySyntax node)
        {
            var left = this.Visit(node.Left);
            var right = this.Visit(node.Right);
            return left == node.Left && right == node.Right
                ? node
                : EnsureExpression(left).BinaryOp(node.Operator, EnsureExpression(right));
        }

        protected internal virtual ODataExpressionSyntax VisitUnary(ODataUnarySyntax node)
        {
            var operand = this.Visit(node.Operand);
            return operand == node.Operand
                ? node
                : EnsureExpression(operand).UnaryOp(node.Operator);
        }

        protected internal virtual ODataExpressionSyntax VisitConstant(ODataConstantSyntax node)
        {
            return node;
        }

        protected internal virtual ODataExpressionSyntax VisitCall(ODataCallSyntax node)
        {
            var arguments = this.VisitList(node.Arguments);
            return arguments == node.Arguments
                ? node
                : ODataSyntaxFactory.Call(node.Function, arguments);
        }

        protected internal virtual ODataMemberSyntax VisitMember(ODataMemberSyntax node)
        {
            var expression = this.Visit(node.Expression);
            return expression == node.Expression
                ? node
                : Ensure<ODataMemberSyntax>(expression).Member(node.Member); 
        }

        protected internal virtual ODataQuerySyntax VisitQuery(ODataQuerySyntax node)
        {
            var filter = this.Visit(node.Filter);
            return filter == node.Filter
                ? node
                : ODataSyntaxFactory.Query(
                    filter: EnsureExpression(filter),
                    top: node.Top,
                    skip: node.Skip
                );
        }

        #region ---- Helpers ----
        private IReadOnlyList<TSyntaxNode> VisitList<TSyntaxNode>(IReadOnlyList<TSyntaxNode> nodes)
            where TSyntaxNode : ODataSyntaxNode
        {
            TSyntaxNode[] visitedNodes = null;
            for (var i = 0; i < nodes.Count; ++i)
            {
                var node = nodes[i];
                var visitedNode = this.Visit(node);
                if (visitedNode == node)
                {
                    if (visitedNodes != null)
                    {
                        visitedNodes[i] = node;
                    }
                }
                else
                {
                    if (visitedNodes != null)
                    {
                        visitedNodes = new TSyntaxNode[nodes.Count];
                        for (var j = 0; j < i; ++j)
                        {
                            visitedNodes[j] = nodes[j];
                        }
                    }
                    visitedNodes[i] = Ensure<TSyntaxNode>(visitedNode);
                }
            }

            return visitedNodes ?? nodes;
        }

        private static ODataExpressionSyntax EnsureExpression(ODataSyntaxNode node)
        {
            return Ensure<ODataExpressionSyntax>(node);
        }

        private static TSyntaxNode Ensure<TSyntaxNode>(ODataSyntaxNode node)
            where TSyntaxNode : ODataSyntaxNode
        {
            var converted = node as TSyntaxNode;
            if (converted == null && node != null)
            {
                throw new InvalidOperationException("Visiting a syntax node of type " + node.GetType() + " must return a result of type " + typeof(TSyntaxNode));
            }

            return converted;
        }
        #endregion
    }
}
