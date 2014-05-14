using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    internal sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _replacement;
        public ParameterReplacer(ParameterExpression parameter, Expression replacement)
        {
            this._parameter = parameter;
            this._replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == this._parameter
                ? this._replacement
                : base.VisitParameter(node);
        }
    }
}
