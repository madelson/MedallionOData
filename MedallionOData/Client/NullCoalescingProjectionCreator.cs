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
    /// Adds null-coalescing behavior to an expression
    /// </summary>
    internal sealed class NullCoalescingAdder : ExpressionVisitor
    {
        // property access -> a.B => (a == null ? null : a.B)
        // instance method call a.B(...) => a == null ? null : a.B(...) or, at top lelve
        // projection to anonymous type (constructor projection) new { a.Company, a.Id } => if not top-level may need to re-generic the type?
            // gets hard to re-generic with lambda invocations, unless we also do Invoke inlining
        // special handling for .Value, .HasValue

        private bool _isTopLevel = true;
        private NullCoalescingAdder() { }

        public static Expression AddNullCoalescing(Expression expression)
        {
            return new NullCoalescingAdder().Visit(expression);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == null)
            {
                // static members don't get modified
                return base.VisitMember(node);
            }

            var isTopLevel = this._isTopLevel;
            this._isTopLevel = false;
            var instance = this.Visit(node.Expression);
            this._isTopLevel = isTopLevel;
        }
    }
}
