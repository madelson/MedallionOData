using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;

namespace Medallion.OData.Service.Sql
{
    internal enum BoolMode
    {
        Bit,
        Bool,
    }

    internal static class BoolModeHelper
    {
        /// <summary>
        /// Gets the default SQL boolean type of the given expression
        /// </summary>
        public static BoolMode GetDefaultMode(ODataExpression expression)
        {
            switch (expression.Kind) 
            {
                case ODataExpressionKind.BinaryOp:
                    switch (((ODataBinaryOpExpression)expression).Operator) 
                    {
                        case ODataBinaryOp.And:
                        case ODataBinaryOp.Or:
                        case ODataBinaryOp.LessThan:
                        case ODataBinaryOp.LessThanOrEqual:
                        case ODataBinaryOp.Equal:
                        case ODataBinaryOp.NotEqual:
                        case ODataBinaryOp.GreaterThanOrEqual:
                        case ODataBinaryOp.GreaterThan:
                            return BoolMode.Bool;
                        default:
                            return BoolMode.Bit;
                    }
                case ODataExpressionKind.UnaryOp:
                    var unaryOp = ((ODataUnaryOpExpression)expression).Operator;
                    switch (unaryOp)
                    {
                        case ODataUnaryOp.Not:
                            return BoolMode.Bool;
                        default:
                            throw Throw.UnexpectedCase(unaryOp);
                    }
                case ODataExpressionKind.Call:
                    return expression.Type == ODataExpressionType.Boolean
                        // a SQL cast to bool is a cast to bit, so the render mode is bit
                        && ((ODataCallExpression)expression).Function != ODataFunction.Cast
                        ? BoolMode.Bool
                        : BoolMode.Bit;
                default:
                    return BoolMode.Bit;
            }
        }
    }
}
