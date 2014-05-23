using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;

namespace Medallion.OData.Parser
{
	internal class ODataExpressionLanguageParser
	{
		// precedence described by https://tools.oasis-open.org/issues/browse/ODATA-203
		// grouping, unary, multiplicative, then additive, relational and type testing, equality, AND, OR
		// group = ( expression )
		// call = id ( expressionList )
		// memberaccess = id [/ id]*
		// simple = [literal | call | memberaccess | group]
		// unary = [not]? simple
		// factor = unary [[+ | -] unary]*
		// term = factor [[* | / | %] factor]*
		// comparison = term [[eq | ne | ...]* term]
		// andExpression = comparison [and comparison]*
		// orExpression = andExpression [or andExpression]*
		// expression = orExpression
		// expressionList = expression [, expression]*

		private readonly IReadOnlyList<ODataToken> _tokens;
		private readonly Type _elementType;
		private int _counter;

		public ODataExpressionLanguageParser(Type elementType, string text)
		{
			Throw.IfNull(elementType, "elementType");
			Throw.IfNull(text, "text");

			this._elementType = elementType;
			this._tokens = ODataExpressionLanguageTokenizer.Tokenize(text);
		}

		#region ---- Helpers ----
		private ODataToken Next(int lookahead = 1)
		{
			var index = this._counter + lookahead - 1;
			return index >= this._tokens.Count 
				? this._tokens[this._tokens.Count - 1] 
				: this._tokens[index];
		}
		
		private bool TryEat(ODataTokenKind kind, out ODataToken next)
		{
			if (this.Next().Kind == kind)
			{
				next = this.Next();
				this._counter++;
				return true;
			}
			next = null;
			return false;
		}

		private bool TryEat(ODataTokenKind kind)
		{
			ODataToken next;
			return this.TryEat(kind, out next);
		}

		private ODataToken Eat(ODataTokenKind kind)
		{
			ODataToken next;
			if (!this.TryEat(kind, out next))
			{
				throw new ODataParseException(string.Format("Expected token of type {0}, but found {1}", kind, this.Next()));
			}
			return next;
		}

		private ODataExpression ParseBinaryExpressionHelper(Func<ODataExpression> parse, IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> binaryOpMapping)
		{
			var result = parse();
			OUTER: 
			while (true)
			{
				foreach (var kvp in binaryOpMapping)
				{
					if (this.TryEat(kvp.Key))
					{
						result = ODataExpression.BinaryOp(result, kvp.Value, parse());
						goto OUTER;
					}
				}
				return result;
			}
		}
		#endregion

		#region ---- Public API ----
		public ODataExpression Parse()
		{
			return this.Parse(this.ParseExpression);
		}

		public IReadOnlyList<ODataSortKeyExpression> ParseSortKeyList()
		{
			return this.Parse(() => this.ParseExpressionList(this.ParseSortKey, ODataTokenKind.Comma));
		}

		public IReadOnlyList<ODataSelectColumnExpression> ParseSelectColumnList()
		{
			return this.Parse(() => this.ParseExpressionList(this.ParseSelectColumn, ODataTokenKind.Comma));
		} 
		#endregion

		#region ---- Parse methods ----
		private T Parse<T>(Func<T> startMethod)
		{
			Throw<InvalidOperationException>.If(this._counter != 0, "The Parser has already consumed it's token stream!");

			var result = startMethod();
			this.Eat(ODataTokenKind.Eof);
			return result;
		}

		private IReadOnlyList<TExpression> ParseExpressionList<TExpression>(Func<TExpression> elementParser, ODataTokenKind delimeter)
			where TExpression : ODataExpression
		{
			var elements = new List<TExpression> { elementParser() };
			while (this.TryEat(delimeter))
			{
				elements.Add(elementParser());
			}
			return elements.AsReadOnly();
		}

		private ODataExpression ParseExpression()
		{
			return this.ParseOrExpression();
		}

		private static readonly IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> OrMap = new Dictionary<ODataTokenKind, ODataBinaryOp> { { ODataTokenKind.Or, ODataBinaryOp.Or } }; 
		private ODataExpression ParseOrExpression()
		{
			return this.ParseBinaryExpressionHelper(this.ParseAndExpression, OrMap);
		}

		private static readonly IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> AndMap = new Dictionary<ODataTokenKind, ODataBinaryOp> { { ODataTokenKind.And, ODataBinaryOp.And } }; 
		private ODataExpression ParseAndExpression()
		{
			return this.ParseBinaryExpressionHelper(this.ParseComparison, AndMap);
		}

		private static readonly IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> ComparisonMap = new Dictionary<ODataTokenKind, ODataBinaryOp>
		{
			{ ODataTokenKind.Eq, ODataBinaryOp.Equal },
 			{ ODataTokenKind.Ne, ODataBinaryOp.NotEqual },
			{ ODataTokenKind.Gt, ODataBinaryOp.GreaterThan },
			{ ODataTokenKind.Ge, ODataBinaryOp.GreaterThanOrEqual },
			{ ODataTokenKind.Lt, ODataBinaryOp.LessThan },
			{ ODataTokenKind.Le, ODataBinaryOp.LessThanOrEqual },
		};
		private ODataExpression ParseComparison()
		{
			return this.ParseBinaryExpressionHelper(this.ParseTerm, ComparisonMap);
		}

		private static readonly IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> TermMap = new Dictionary<ODataTokenKind, ODataBinaryOp>
		{
			{ ODataTokenKind.Add, ODataBinaryOp.Add },
 			{ ODataTokenKind.Sub, ODataBinaryOp.Subtract },
		};
		private ODataExpression ParseTerm()
		{
			return this.ParseBinaryExpressionHelper(this.ParseFactor, TermMap);
		}

		private static readonly IReadOnlyDictionary<ODataTokenKind, ODataBinaryOp> FactorMap = new Dictionary<ODataTokenKind, ODataBinaryOp>
		{
			{ ODataTokenKind.Mul, ODataBinaryOp.Multiply },
 			{ ODataTokenKind.Div, ODataBinaryOp.Divide },
			{ ODataTokenKind.Mod, ODataBinaryOp.Modulo },
		};
		private ODataExpression ParseFactor()
		{
			return this.ParseBinaryExpressionHelper(this.ParseUnary, FactorMap);
		}

		private ODataExpression ParseUnary()
		{
			return this.TryEat(ODataTokenKind.Not)
				? ODataExpression.UnaryOp(this.ParseSimple(), ODataUnaryOp.Not)
				: this.ParseSimple();
		}

		private ODataExpression ParseSimple()
		{
			if (this.TryEat(ODataTokenKind.LeftParen))
			{
				// parse group
				var group = this.ParseExpression();
				this.Eat(ODataTokenKind.RightParen);
				return group;
			}

			ODataToken next;
			if (this.TryEat(ODataTokenKind.Identifier, out next))
			{
				if (this.TryEat(ODataTokenKind.LeftParen))
				{
					// parse function
					ODataFunction function;
					if (!Enum.TryParse(next.Text, ignoreCase: true, result: out function))
					{
						throw new ODataParseException(next.Text + " is not a known ODataFunction!");
					}
					var arguments = this.ParseExpressionList(this.ParseExpression, ODataTokenKind.Comma);
					this.Eat(ODataTokenKind.RightParen);

					if (function == ODataFunction.IsOf || function == ODataFunction.Cast)
					{
						var typeLiteral = this.ReParseAsType(arguments[arguments.Count - 1]);
						var argumentsCopy = arguments.ToArray();
						argumentsCopy[arguments.Count - 1] = ODataExpression.Constant(typeLiteral);
						arguments = argumentsCopy;
					}

					return ODataExpression.Call(function, arguments);
				}

				// parse member access
				var type = this._elementType; // root element type
				ODataMemberAccessExpression access = null; // root member
				while (true)
				{
					// get the property
					var property = type.GetProperty(next.Text, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
					if (property == null)
					{
						throw new ODataParseException("Property '" + next.Text + "' could not be found on type " + type.FullName);
					}
					access = ODataExpression.MemberAccess(access, property);
					
					// if we don't see '/' followed by an identifier, we're done
					// we can't just use TryEat() here, because we need to distinguish between Foo/Bar/* and Foo/Bar/Baz
					if (this.Next().Kind != ODataTokenKind.Slash || this.Next(2).Kind != ODataTokenKind.Identifier)
					{
						break;
					}

					// otherwise, update next to the next id and then advance type down the property chain
					this.Eat(ODataTokenKind.Slash);
					next = this.Eat(ODataTokenKind.Identifier);
					type = property.PropertyType;
				}
				return access;
			}

			// literals
			if (this.TryEat(ODataTokenKind.NullLiteral, out next))
			{
				return ODataExpression.Constant(null);
			}
			if (this.TryEat(ODataTokenKind.BinaryLiteral, out next))
			{
				throw new NotImplementedException("Binary Literal Parse");
			}
			if (this.TryEat(ODataTokenKind.BooleanLiteral, out next))
			{
				return ODataExpression.Constant(bool.Parse(next.Text));
			}
			// see comment on the enum
			//if (this.TryEat(ODataTokenKind.ByteLiteral, out next))
			//{
			//	return ODataExpression.Constant(Convert.ToByte(next.Text, fromBase: 16));
			//}
			if (this.TryEat(ODataTokenKind.DateTimeLiteral, out next))
			{
				Func<string, string> zeroIfEmpty = s => s.Length == 0 ? "0" : s;
				var dateTime = new DateTime(
						year: int.Parse(next.Match.Groups["year"].Value),
						month: int.Parse(next.Match.Groups["month"].Value),
						day: int.Parse(next.Match.Groups["day"].Value),
						hour: int.Parse(next.Match.Groups["hour"].Value),
						minute: int.Parse(next.Match.Groups["minute"].Value),
						second: int.Parse(zeroIfEmpty(next.Match.Groups["second"].Value)),
						millisecond: 0
					)
					.AddSeconds(double.Parse(zeroIfEmpty(next.Match.Groups["fraction"].Value)));
				return ODataExpression.Constant(dateTime);
			}
			if (this.TryEat(ODataTokenKind.DecimalLiteral, out next))
			{
				return ODataExpression.Constant(decimal.Parse(next.Text.Substring(0, next.Text.Length - 1)));
			}
			if (this.TryEat(ODataTokenKind.DoubleLiteral, out next))
			{
				return ODataExpression.Constant(double.Parse(next.Text));
			}
			if (this.TryEat(ODataTokenKind.SingleLiteral, out next))
			{
				return ODataExpression.Constant(float.Parse(next.Text.Substring(0, next.Text.Length - 1)));
			}
			if (this.TryEat(ODataTokenKind.GuidLiteral, out next))
			{
				return ODataExpression.Constant(Guid.Parse(next.Match.Groups["digits"].Value));
			}
			if (this.TryEat(ODataTokenKind.Int32Literal, out next))
			{
				return ODataExpression.Constant(int.Parse(next.Text));
			}
			if (this.TryEat(ODataTokenKind.Int64Literal, out next))
			{
				return ODataExpression.Constant(long.Parse(next.Text.Substring(0, next.Text.Length - 1)));
			}
			if (this.TryEat(ODataTokenKind.StringLiteral, out next))
			{
				// unescaping, from http://stackoverflow.com/questions/3979367/how-to-escape-a-single-quote-to-be-used-in-an-odata-query
				return ODataExpression.Constant(next.Match.Groups["chars"].Value.Replace("''", "'"));
			}

			throw new ODataParseException("Unexpected token " + this.Next());
		}

		private ODataSortKeyExpression ParseSortKey()
		{
			var expression = this.ParseExpression();
			if (this.TryEat(ODataTokenKind.Desc))
			{
				return ODataExpression.SortKey(expression, descending: true);
			}
			this.TryEat(ODataTokenKind.Asc);
			return ODataExpression.SortKey(expression);
		}

		private ODataSelectColumnExpression ParseSelectColumn()
		{
			if (this.TryEat(ODataTokenKind.Star))
			{
				return ODataExpression.SelectStar();
			}
			var expression = this.ParseSimple();
			var memberAccess = expression as ODataMemberAccessExpression;
			if (memberAccess == null)
			{
				throw new ODataParseException(string.Format("Expected member access expression. Found '{0}' ({1})" + expression, expression.Kind));
			}
			if (this.TryEat(ODataTokenKind.Slash))
			{
				this.Eat(ODataTokenKind.Star);
				return ODataExpression.SelectColumn(memberAccess, allColumns: true);
			}
			return ODataExpression.SelectColumn(memberAccess, allColumns: false);
		}

		private Type ReParseAsType(ODataExpression expression)
		{
			if (expression.Kind != ODataExpressionKind.Constant || expression.Type != ODataExpressionType.String)
			{
				throw new ODataParseException("Only a string constant may be parsed as a type. Found a " + expression.Kind + " of type " + expression.Type);
			}

			// search for Edm types
			var value = (string)((ODataConstantExpression)expression).Value;
			var primitiveType = Helpers.GetValuesAndFields<ODataExpressionType>()
				.Select(t => new { @enum = t.Item1, attr = t.Item2.GetCustomAttribute<ODataNameAttribute>() })
				.FirstOrDefault(t => t.attr != null && t.attr.Name.Trim('\'') == value);
			if (primitiveType != null)
			{
				return primitiveType.@enum.ToClrType();
			}

			// search for the type in available assemblies. We could be even more robust
			// by loading all referenced assemblies, but this is probably fine given that
			// any derived entity is likely in the same assembly as the base entity, which 
			// got loaded when this ran
			var type = AppDomain.CurrentDomain.GetAssemblies()
				.Select(a => a.GetType(value))
				.FirstOrDefault(t => t != null);
			if (type != null)
			{
				return type;
			}

			throw new ODataParseException("Could not parse '" + value + "' as a type");
		}
		#endregion
	}
}
