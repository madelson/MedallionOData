using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    public enum ODataSyntaxKind
    {
        Query = 0,

        BinaryOp = 1,
        UnaryOp = 2,
        Call = 3,
        Constant = 4,
        MemberAccess = 5,
        Group = 6,
    }

    public abstract class ODataSyntaxNode
    {
        internal ODataSyntaxNode() { }

        internal abstract ODataSyntaxNode Accept(ODataSyntaxVisitor visitor);

        public abstract ODataSyntaxKind Kind { get; }
    }
}
