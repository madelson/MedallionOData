using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    public static class ODataSyntaxFactory
    {
        public static ODataGroupSyntax Group(this ODataExpressionSyntax expression)
        {
            return new ODataGroupSyntax(expression);
        }

        public static ODataBinarySyntax BinaryOp(this ODataExpressionSyntax left, ODataBinaryOp @operator, ODataExpressionSyntax right)
        {
            return new ODataBinarySyntax(left, @operator, right);
        }

        public static ODataUnarySyntax UnaryOp(this ODataExpressionSyntax operand, ODataUnaryOp @operator)
        {
            return new ODataUnarySyntax(@operator, operand);
        }

        public static ODataConstantSyntax Constant(object value)
        {
            return new ODataConstantSyntax(value);
        }

        public static ODataCallSyntax Call(string function, IEnumerable<ODataExpressionSyntax> arguments)
        {
            return new ODataCallSyntax(function, arguments);
        }

        public static ODataMemberSyntax Member(this ODataMemberSyntax expression, string member)
        {
            return new ODataMemberSyntax(expression, member);
        }

        public static ODataMemberSyntax Member(string member)
        {
            return Member(expression: null, member: member);
        }

        public static ODataQuerySyntax Query(
            ODataExpressionSyntax filter = null,
            int? top = null,
            int skip = 0)
        {
            return new ODataQuerySyntax(
                filter: filter,
                top: top,
                skip: skip
            );
        }
    }
}
