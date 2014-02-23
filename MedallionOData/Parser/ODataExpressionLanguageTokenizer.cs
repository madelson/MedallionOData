using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Medallion.OData.Parser
{
	// http://www.odata.org/documentation/overview/#AbstractDataModel
	internal enum ODataTokenKind
	{
		IntLiteral,
		NullLiteral,
		BinaryLiteral,
		BooleanLiteral,
		// MA: not supporting this right now because it's ambiguous token-wise with identifier and it's not very useful
		//ByteLiteral,
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
		Error,
		Eof,
	}

	internal class ODataToken
	{
		internal ODataToken(Match match, ODataTokenKind kind)
		{
			this.Match = match;
			this.Kind = kind;
		}

		public Match Match { get; private set; }
		public string Text { get { return this.Match.Value; } }
		public ODataTokenKind Kind { get; private set; }

		public override string ToString()
		{
			return this.Kind.ToString().Equals(this.Text, StringComparison.OrdinalIgnoreCase)
				? string.Format("\"{0}\"", this.Text)
				: string.Format("{0}(\"{1}\")", this.Kind, this.Text);
		}
	}

	internal class ODataExpressionLanguageTokenizer
	{
		private static readonly IReadOnlyList<KeyValuePair<ODataTokenKind, string>> Kinds = Helpers.GetValues<ODataTokenKind>()
			.Select(k => KeyValuePair.Create(k, k.ToString()))
			.ToImmutableList();
		private static readonly Regex TokenizerRegex;

        private class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 t1, T2 t2) { this.Add(Tuple.Create(t1, t2)); }
        }

		static ODataExpressionLanguageTokenizer()
		{
			var tokenToResx = new TupleList<ODataTokenKind, string>
			{
				{ ODataTokenKind.NullLiteral, "null" },
				{ ODataTokenKind.BinaryLiteral, "(binary|X)'[A-Fa-f0-9]+'" },
				{ ODataTokenKind.BooleanLiteral, "true|false" },
				// see comment on the enum
				//{ ODataTokenKind.ByteLiteral, "[A-Fa-f0-9]+" },
				{ ODataTokenKind.DateTimeLiteral, @"datetime'(?<year>\d\d\d\d)-(?<month>\d\d)-(?<day>\d\d)T(?<hour>\d\d):(?<minute>\d\d)(:(?<second>\d\d)((?<fraction>\.\d+))?)?'" },
				{ ODataTokenKind.Int64Literal, "-?[0-9]+L" },
				{ ODataTokenKind.DecimalLiteral, @"-?[0-9]+(\.[0-9]+)?(M|m)" },
				{ ODataTokenKind.SingleLiteral, @"-?[0-9]+\.[0-9]+f" },
				{ ODataTokenKind.DoubleLiteral, @"-?[0-9]+((\.[0-9]+)|(E[+-]?[0-9]+))" },
				{ ODataTokenKind.Int32Literal, "-?[0-9]+" },
				{ ODataTokenKind.GuidLiteral, @"guid'(?<digits>DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD)'".Replace("D", "[A-Fa-f0-9]") },
				{ ODataTokenKind.StringLiteral, "'(?<chars>(''|[^'])*)'"},
				{ ODataTokenKind.Eq, @"eq(?=\s|$)" },
				{ ODataTokenKind.Ne, @"ne(?=\s|$)" },
				{ ODataTokenKind.Gt, @"gt(?=\s|$)" },
				{ ODataTokenKind.Ge, @"ge(?=\s|$)" },
				{ ODataTokenKind.Lt, @"lt(?=\s|$)" },
				{ ODataTokenKind.Le, @"le(?=\s|$)" },
				{ ODataTokenKind.And, @"and(?=\s|$)" },
				{ ODataTokenKind.Or, @"or(?=\s|$)" },
				{ ODataTokenKind.Not, @"not(?=\s|$)" },
				{ ODataTokenKind.Add, @"add(?=\s|$)" },
				{ ODataTokenKind.Sub, @"sub(?=\s|$)" },
				{ ODataTokenKind.Mul, @"mul(?=\s|$)" },
				{ ODataTokenKind.Div, @"div(?=\s|$)" },
				{ ODataTokenKind.Mod, @"mod(?=\s|$)" },
				{ ODataTokenKind.Asc, @"asc(?=\s|,|$)" },
				{ ODataTokenKind.Desc, @"desc(?=\s|,|$)" },
				// TODO time, date-time offset
				{ ODataTokenKind.LeftParen, @"\(" },
				{ ODataTokenKind.RightParen, @"\)" },
				{ ODataTokenKind.Star, @"\*" },
				{ ODataTokenKind.Identifier, @"[a-zA-z_][a-zA-Z_0-9]*" },
				{ ODataTokenKind.WhiteSpace, @"\s+" },
				{ ODataTokenKind.Comma, "," },
				{ ODataTokenKind.Slash, "/" },
				{ ODataTokenKind.Error, @"." },
			};	

			TokenizerRegex = new Regex(
				tokenToResx.Select(t => string.Format("(?<{0}>{1})", t.Item1, t.Item2))
					.ToDelimitedString("|"),
				RegexOptions.ExplicitCapture
			);
		}

		public static List<ODataToken> Tokenize(string text)
		{
			var tokens = TokenizerRegex.Matches(text)
				.Cast<Match>()
				.Select(m =>
				{
					var kind = Kinds.First(kvp => !string.IsNullOrEmpty(m.Groups[kvp.Value].Value));
					return new ODataToken(m, kind.Key);
				})
				.Where(t => t.Kind != ODataTokenKind.WhiteSpace)
				.ToList();
			tokens.Add(new ODataToken(null, ODataTokenKind.Eof));
			return tokens;
		}
	}
}
