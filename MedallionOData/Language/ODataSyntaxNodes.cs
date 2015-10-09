using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    #region ---- Query ----
    public sealed class ODataQuerySyntax : ODataSyntaxNode
    {
        public ODataQuerySyntax(ODataExpressionSyntax filter, int? top, int skip)
        {
            this.Filter = filter;
            this.Top = top;
            this.Skip = skip;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.Query;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitQuery(this);

        public ODataExpressionSyntax Filter { get; }
        public int? Top { get; }
        public int Skip { get; }
    }
    #endregion

    #region ---- Expression ----
    public abstract class ODataExpressionSyntax : ODataSyntaxNode
    {
        internal ODataExpressionSyntax() { }
    }
    #endregion

    // TODO may want to remove this
    #region ---- Group ----
    public sealed class ODataGroupSyntax : ODataExpressionSyntax
    {
        internal ODataGroupSyntax(ODataExpressionSyntax expression)
        {
            Throw.IfNull(expression, nameof(expression));
            this.Expression = expression;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.Group;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitGroup(this);

        public ODataExpressionSyntax Expression { get; }
    }
    #endregion

    #region ---- Binary ----
    public sealed class ODataBinarySyntax : ODataExpressionSyntax
    {
        internal ODataBinarySyntax(ODataExpressionSyntax left, ODataBinaryOp @operator, ODataExpressionSyntax right)
        {
            Throw.IfNull(left, nameof(left));
            Throw.IfNull(right, nameof(right));

            this.Left = left;
            this.Operator = @operator;
            this.Right = right;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.BinaryOp;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitBinary(this);

        public ODataExpressionSyntax Left { get; }
        public ODataBinaryOp Operator { get; }
        public ODataExpressionSyntax Right { get; }
    }
    #endregion

    #region ---- Unary ----
    public sealed class ODataUnarySyntax : ODataExpressionSyntax
    {
        internal ODataUnarySyntax(ODataUnaryOp @operator, ODataExpressionSyntax operand)
        {
            Throw.IfNull(operand, nameof(operand));

            this.Operator = @operator;
            this.Operand = operand;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.UnaryOp;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitUnary(this);

        public ODataUnaryOp Operator { get; }
        public ODataExpressionSyntax Operand { get; }
    }
    #endregion

    #region ---- Constant ----
    public sealed class ODataConstantSyntax : ODataExpressionSyntax
    {
        internal ODataConstantSyntax(object value)
        {
            this.Value = value;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.Constant;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitConstant(this);

        public object Value { get; }
    }
    #endregion

    #region ---- Call ----
    // TODO boundFunctionExpr (really just need to record "isbound" here)
    public sealed class ODataCallSyntax : ODataExpressionSyntax
    {
        internal ODataCallSyntax(string function, IEnumerable<ODataExpressionSyntax> arguments)
        {
            Throw.IfNull(function, nameof(function));
            Throw.IfNull(arguments, nameof(arguments));
            var argumentsArray = arguments.ToArray();
            Throw.If(argumentsArray.Contains(null), nameof(arguments) + ": must not contain nulls");

            this.Function = function;
            this.Arguments = argumentsArray;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.Call;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitCall(this);

        public string Function { get; }
        public IReadOnlyList<ODataExpressionSyntax> Arguments { get; }
    }
    #endregion

    #region ---- Member ----
    public sealed class ODataMemberSyntax : ODataExpressionSyntax
    {
        internal ODataMemberSyntax(ODataMemberSyntax expression, string member)
        {
            // note: expression is allowed to be null: that's how you access root members
            Throw.IfNull(member, nameof(member));

            this.Expression = expression;
            this.Member = member;
        }

        public override ODataSyntaxKind Kind => ODataSyntaxKind.MemberAccess;
        internal override ODataSyntaxNode Accept(ODataSyntaxVisitor visitor) => visitor.VisitMember(this);

        public ODataMemberSyntax Expression { get; }
        public string Member { get; }
    }
    #endregion
}
