using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    internal enum ODataTokenKind
    {
        NullLiteral,
        BinaryLiteral,
        BooleanLiteral,
        DateTimeLiteral,
        DecimalLiteral,
        DoubleLiteral,
        SingleLiteral,
        GuidLiteral,
        Int32Literal,
        Int64Literal,
        StringLiteral,
        TimeLiteral,
        DateTimeOffsetLiteral,

        Eq,
        Ne,
        Gt,
        Ge,
        Lt,
        Le,
        And,
        Or,
        Not,
        Add,
        Sub,
        Mul,
        Div,
        Mod,

        Asc,
        Desc,

        Identifier,
        LeftParen,
        RightParen,
        Comma,
        Slash,
        Star,

        WhiteSpace,
        /// <summary>
        /// Represents an unexpected character
        /// </summary>
		Error,
        /// <summary>
        /// Represents the end of the token stream
        /// </summary>
		Eof,
    }

    internal class ODataToken
    {
        private readonly Match match;

        private ODataToken(ODataTokenKind kind, Match match)
        {
            this.Kind = kind;
            this.match = match;
        }

        public ODataTokenKind Kind { get; }
        public string Text { get { return this.match.Value; } }
        public virtual object Value
        {
            get { return this.Kind == ODataTokenKind.NullLiteral ? null : this.Text; }
        }

        private sealed class TypedODataToken<TValue> : ODataToken
        {
            public TypedODataToken(ODataTokenKind kind, Match match, TValue value)
                : base(kind, match)
            {
                this.Value = value;
            }

            public override object Value { get; }
        }

        public static ODataToken Create(ODataTokenKind kind, Match match)
        {
            return new ODataToken(kind, match);
        }

        public static ODataToken Create<TValue>(ODataTokenKind kind, Match match, TValue value)
        {
            return new TypedODataToken<TValue>(kind, match, value);
        }
    }
}
