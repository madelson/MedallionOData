using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    /// <summary>
    /// MA: aids in constructing a minimal client-side projection expression to be applied on the results
    /// that come back from the remote service
    /// </summary>
    internal sealed class ClientSideProjectionBuilder : ExpressionVisitor
    {
        private static readonly ExpressionVisitor Visitor = new ClientSideProjectionBuilder();

        private ClientSideProjectionBuilder() { }

        /// <summary>
        /// Composes the given projection lambdas into a single projection which only references the
        /// necessary selected colums. Thus, if you have something like
        /// <code>
        ///     query.Select(c => new { CompanyName = c.Company.Name, c.Name })
        ///         .Where(t => t.CompanyName != null &gt;&gt; t.CompanyName.Length > 0)
        ///         .Select(t => t.Name);
        /// </code>
        /// we might naively generate a projection like c => new { CompanyName = c.Company.Name, c.Name }.Name.
        /// This will fail when executed, because we will not have selected any columns from Company.
        /// </summary>
        public static LambdaExpression CreateProjection(IReadOnlyList<LambdaExpression> projections)
        {
            Throw.If(projections.Count == 0, "projections: at least one projection is required");
            Throw.If(projections.Any(e => e == null || e.Parameters.Count != 1), "projections: all expressions must be projections");

            // first, create a composition of the lambdas with all parameters inlined
            var composedProjection = ComposeWithInlining(projections, indexToMerge: projections.Count - 1);

            // next, replace constructs like
            // x = new { a = p.Foo.Bar, b = 2 * p.Name.Length }.b with x = 2 * p.Name.Length
            var simplifiedProjection = (LambdaExpression)Visitor.Visit(composedProjection);

            return simplifiedProjection;
        }

        private static LambdaExpression ComposeWithInlining(IReadOnlyList<LambdaExpression> projections, int indexToMerge)
        {
            var currentProjection = projections[indexToMerge];

            if (indexToMerge == 0)
            {
                return currentProjection;
            }

            // construct the merger of all previous projections
            var previousProjection = ComposeWithInlining(projections, indexToMerge - 1);
            Throw.If(!currentProjection.Parameters[0].Type.IsAssignableFrom(previousProjection.ReturnType), "projections: cannot be chained due to type mismatch");

            // merge the current with the previous by inlining
            var replacer = new ParameterReplacer(currentProjection.Parameters[0], previousProjection.Body);
            var inlinedBody = replacer.Visit(currentProjection.Body);
            return Expression.Lambda(inlinedBody, previousProjection.Parameters);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // first, we call the base visit method. This is required to deal with nested member accesses
            // such as new { a = new { b = 2 } }.a.b. If we translate first, we won't be able to do anything with b
            // initially, so we'll be left with new { b = 2 }.b. However, if we do base visit first, we'll first
            // change to new { b = 2 }.b at which point we can translate to 2
            var visited = (MemberExpression)base.VisitMember(node);

            // this check is to skip static members
            if (visited.Expression != null
                // this check is to avoid looking at things like string.Length
                && visited.Expression.Type.ToODataExpressionType() == ODataExpressionType.Complex)
            {
                switch (visited.Expression.NodeType)
                {
                    case ExpressionType.New:
                        if (!visited.Expression.Type.IsAnonymous())
                        {
                            goto default;
                        }
                        var @new = (NewExpression)visited.Expression;

                        // replace new { a = expr ... }.a with Visit(expr)
                        var memberParameterIndex = @new.Constructor.GetParameters()
                            .IndexWhere(p => p.Name == visited.Member.Name && p.ParameterType == visited.Type);
                        return this.Visit(@new.Arguments[memberParameterIndex]);
                    case ExpressionType.MemberInit:
                        var memberInit = (MemberInitExpression)visited.Expression;
                        if (memberInit.NewExpression.Arguments.Count > 0
                            || memberInit.Bindings.Any(mb => mb.BindingType != MemberBindingType.Assignment))
                        {
                            goto default;
                        }

                        // replace new X { A = expr, ... }.A with Visit(expr)
                        var matchingBinding = memberInit.Bindings.Cast<MemberAssignment>()
                            .FirstOrDefault(mb => Helpers.MemberComparer.Equals(mb.Member, visited.Member));
                        if (matchingBinding == null)
                        {
                            throw new ODataCompileException(string.Format("Member {0} was accessed but was not initialized", node.Member));
                        }
                        return this.Visit(matchingBinding.Expression);
                    default:
                        break;
                }
            }

            return visited;
        }
    }
}
