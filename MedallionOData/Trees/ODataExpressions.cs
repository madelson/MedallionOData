using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Trees
{
    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.BinaryOp"/>
    /// </summary>
	public sealed class ODataBinaryOpExpression : ODataExpression
	{
		internal ODataBinaryOpExpression(ODataExpression left, ODataBinaryOp @operator, ODataExpression right, Type clrType)
			: base(ODataExpressionKind.BinaryOp, clrType.ToODataExpressionType(), clrType)
		{
			Throw<ArgumentException>.If(right.Type != left.Type, "right & left: must have equal types");
			this.Right = right;
			this.Operator = @operator;
			this.Left = left;
		}

        /// <summary>The right-hand expression</summary>
		public ODataExpression Right { get; private set; }
        /// <summary>The operator</summary>
		public ODataBinaryOp Operator { get; private set; }
        /// <summary>The left-hand expression</summary>
        public ODataExpression Left { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			return new StringBuilder()
				.AppendFormat(this.NeedsParens(this.Left) ? "({0}) " : "{0} ", this.Left)
				.Append(this.Operator.ToODataString())
				.AppendFormat(this.NeedsParens(this.Right) ? " ({0})" : " {0}", this.Right)
				.ToString();
		}

		private bool NeedsParens(ODataExpression leftOrRight)
		{
			if (leftOrRight.Kind != ODataExpressionKind.BinaryOp)
			{
				return false;
			}
			var binaryOp = (ODataBinaryOpExpression)leftOrRight;
			switch (binaryOp.Operator)
			{
				case ODataBinaryOp.Or:
					return this.Operator > ODataBinaryOp.Or;

				case ODataBinaryOp.And:
				case ODataBinaryOp.Equal:
				case ODataBinaryOp.NotEqual:
				case ODataBinaryOp.GreaterThan:
				case ODataBinaryOp.GreaterThanOrEqual:
				case ODataBinaryOp.LessThan:
				case ODataBinaryOp.LessThanOrEqual:
					return this.Operator > ODataBinaryOp.And;

				case ODataBinaryOp.Add: 
				case ODataBinaryOp.Subtract:
				case ODataBinaryOp.Multiply:
				case ODataBinaryOp.Divide:
				case ODataBinaryOp.Modulo:
					return this.Operator > ODataBinaryOp.Subtract;

				default:
					throw Throw.UnexpectedCase(binaryOp);
			}
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.UnaryOp"/>
    /// </summary>
    public sealed class ODataUnaryOpExpression : ODataExpression
	{
		internal ODataUnaryOpExpression(ODataExpression operand, ODataUnaryOp @operator)
            // in general, unary ops don't change the type. If we ever implement one that does, we can change this logic
			: base(ODataExpressionKind.UnaryOp, operand.Type, operand.ClrType)
		{
			this.Operand = operand;
			this.Operator = @operator;
		}

        /// <summary>The operand</summary>
		public ODataExpression Operand { get; private set; }
        /// <summary>The operator</summary>
		public ODataUnaryOp Operator { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
            // we need parens if the operand is lower priority than the operator
            var needsParens = this.Operand.Kind == ODataExpressionKind.BinaryOp;

			return string.Format(needsParens ? "{0}({1})" : "{0} {1}", this.Operator.ToODataString(), this.Operand);
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.Call"/>
    /// </summary>
    public sealed class ODataCallExpression : ODataExpression
	{
		internal ODataCallExpression(ODataFunction function, IReadOnlyList<ODataExpression> arguments, Type returnClrType)
			: base(ODataExpressionKind.Call, returnClrType.ToODataExpressionType(), returnClrType)
		{
			this.Function = function;
			this.Arguments = arguments;
		}

        /// <summary>the function call</summary>
		public ODataFunction Function { get; private set; }
        /// <summary>the arguments</summary>
		public IReadOnlyList<ODataExpression> Arguments { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			return string.Format("{0}({1})", this.Function.ToODataString(), this.Arguments.ToDelimitedString(", "));
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.Constant"/>
    /// </summary>
    public sealed class ODataConstantExpression : ODataExpression
	{
		internal ODataConstantExpression(object value, ODataExpressionType type, Type clrType)
			: base(ODataExpressionKind.Constant, type, clrType)
		{
			this.Value = value;
		}

        /// <summary>the value of the constant</summary>
		public object Value { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			if (this.Value == null)
			{
				return "null";
			}
			switch (this.Type)
			{
				case ODataExpressionType.Binary:
					throw new NotImplementedException("Binary");
				case ODataExpressionType.Boolean:
					return (bool)this.Value ? "true" : "false";
				case ODataExpressionType.Byte:
					return System.Convert.ToString((byte)this.Value, toBase: 16);
				case ODataExpressionType.DateTime:
					// the datetime format & regex is very precise. This logic ensures round-tripping of dates
					var dt = (DateTime)this.Value;
					var dateTimeBuilder = new StringBuilder()
						.AppendFormat("datetime'{0:0000}-{1:00}-{2:00}T{3:00}:{4:00}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute);
					var seconds = (dt.TimeOfDay - new TimeSpan(hours: dt.Hour, minutes: dt.Minute, seconds: 0)).TotalSeconds;
					if (seconds > 0)
					{
						dateTimeBuilder.AppendFormat(":{0:00}", dt.Second);
						if (seconds > dt.Second)
						{
							dateTimeBuilder.AppendFormat("{0:.0000000}", seconds - dt.Second);
						}
					}
					return dateTimeBuilder.Append('\'').ToString();
				case ODataExpressionType.Decimal:
					return this.Value + "M";
				case ODataExpressionType.Double:
					return this.Value.ToString();
				case ODataExpressionType.Guid:
					return string.Format("guid'{0}'", this.Value);
				case ODataExpressionType.Int16:
				case ODataExpressionType.Int32:
					return this.Value.ToString();
				case ODataExpressionType.Int64:
					return this.Value + "L";
				case ODataExpressionType.Single:
					return ((float)this.Value).ToString("0.0") + "f";
				case ODataExpressionType.String:
					// escaping as in http://stackoverflow.com/questions/3979367/how-to-escape-a-single-quote-to-be-used-in-an-odata-query
					return string.Format("'{0}'", ((string)this.Value).Replace("'", "''"));
				case ODataExpressionType.Type:
					var clrType = (Type)this.Value;
					var oDataType = clrType.ToODataExpressionType();
					if (oDataType.IsPrimitive())
					{
						return oDataType.ToODataString();
					}
					return string.Format("'{0}'", clrType);
				default:
					throw Throw.UnexpectedCase(this.Type);
			}
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.MemberAccess"/>
    /// </summary>
    public sealed class ODataMemberAccessExpression : ODataExpression
	{
		internal ODataMemberAccessExpression(ODataMemberAccessExpression expression, PropertyInfo member)
			: base(ODataExpressionKind.MemberAccess, member.PropertyType.ToODataExpressionType(), member.PropertyType)
		{
			this.Expression = expression;
			this.Member = member;
		}

        /// <summary>the expression whose member is being accessed</summary>
		public ODataMemberAccessExpression Expression { get; private set; }
        /// <summary>the property being accessed</summary>
		public PropertyInfo Member { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			return this.Expression != null
				? string.Format("{0}/{1}", this.Expression, this.Member.Name)
				: this.Member.Name;
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.Convert"/>
    /// </summary>
    public sealed class ODataConvertExpression : ODataExpression
	{
		internal ODataConvertExpression(ODataExpression expression, Type clrType)
			: base(ODataExpressionKind.Convert, clrType.ToODataExpressionType(), clrType)
		{
			this.Expression = expression;
		}

        /// <summary>the expression being converted</summary>
		public ODataExpression Expression { get; private set; }

		internal override string ToODataExpressionLanguage()
		{			
			return this.Expression.Type.IsImplicityCastableTo(this.Type)
				? this.Expression.ToString()
				: string.Format("cast({0}, {1})", this.Expression, this.Type.ToODataString());
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.SortKey"/>
    /// </summary>
    public sealed class ODataSortKeyExpression : ODataExpression
	{
		internal ODataSortKeyExpression(ODataExpression expression, bool descending)
			: base(ODataExpressionKind.SortKey, expression.Type, expression.ClrType)
		{
			this.Expression = expression;
			this.Descending = descending;
		}

        /// <summary>the value to sort by</summary>
		public ODataExpression Expression { get; private set; }
        /// <summary>specifies the direction of the sort</summary>
        public bool Descending { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			return this.Expression + (this.Descending ? " desc" : string.Empty);
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.SelectColumn"/>
    /// </summary>
    public sealed class ODataSelectColumnExpression : ODataExpression
	{
		internal ODataSelectColumnExpression(ODataMemberAccessExpression expression, bool allColumns)
            // TODO FUTURE we have to pass typeof(object) here for select *. We could fix this with a "ParameterExpression" type
			: base(ODataExpressionKind.SelectColumn, expression != null ? expression.Type : ODataExpressionType.Complex, expression != null ? expression.ClrType : typeof(object))
		{
			this.Expression = expression;
			this.AllColumns = allColumns;
		}

        /// <summary>
        /// The member being selected
        /// </summary>
		public ODataMemberAccessExpression Expression { get; private set; }
		/// <summary>
		/// Are all columns being selected?
		/// </summary>
        public bool AllColumns { get; private set; }

		internal override string ToODataExpressionLanguage()
		{
			var sb = new StringBuilder().Append(this.Expression);
			if (this.AllColumns)
			{
				if (this.Expression != null)
				{
					sb.Append('/');
				}
				sb.Append('*');
			}
			return sb.ToString();
		}
	}

    /// <summary>
    /// An expression for <see cref="ODataExpressionKind.Query"/>
    /// </summary>
	public class ODataQueryExpression : ODataExpression
	{
		// TODO VNEXT expand
		internal ODataQueryExpression(
			ODataExpression filter,
			IReadOnlyList<ODataSortKeyExpression> orderBy,
			int? top,
			int skip,
			string format,
			ODataInlineCountOption inlineCount,
			IReadOnlyList<ODataSelectColumnExpression> select)
			: base(ODataExpressionKind.Query, ODataExpressionType.Complex, typeof(IQueryable))
		{
			this.Filter = filter;
			this.OrderBy = orderBy;
			this.Top = top;
			this.Skip = skip;
			this.Format = format;
			this.InlineCount = inlineCount;
			this.Select = select;
		}

        /// <summary>the expression to filter by</summary>
		public ODataExpression Filter { get; private set; }
        /// <summary>the list of sort keys</summary>
        public IReadOnlyList<ODataSortKeyExpression> OrderBy { get; private set; }
        /// <summary>the number of items to take</summary>
        public int? Top { get; private set; }
        /// <summary>the number to skip</summary>	
		public int Skip { get; private set; }
        /// <summary>the format to use</summary>	
		public string Format { get; private set; }
        /// <summary>the inline count option to use</summary>
		public ODataInlineCountOption InlineCount { get; private set; }
        /// <summary>the columns to select</summary>		
		public IReadOnlyList<ODataSelectColumnExpression> Select { get; private set; }
	
		internal NameValueCollection ToNameValueCollection()
		{
			var result = new NameValueCollection();
			if (this.Filter != null)
			{
				result.Add("$filter", this.Filter.ToString());
			}
			if (this.OrderBy.Count > 0)
			{
				result.Add("$orderby", this.OrderBy.ToDelimitedString());
			}
			if (this.Top.HasValue)
			{
				result.Add("$top", this.Top.ToString());
			}
			if (this.Skip != 0)
			{
				result.Add("$skip", this.Skip.ToString());
			}
			if (this.Format != null)
			{
				result.Add("$format", this.Format);
			}
			if (this.InlineCount != ODataInlineCountOption.None)
			{
				result.Add("$inlinecount", this.InlineCount.ToODataString());
			}
			if (this.Select.Count > 0)
			{
				result.Add("$select", this.Select.ToDelimitedString());
			}

			return result;
		}

		internal override string ToODataExpressionLanguage()
		{
			var builder = new StringBuilder("?");
			var values = this.ToNameValueCollection();
			foreach (string key in values)
			{
				AppendParam(builder, key, values[key]);
			}

			return builder.ToString();
		}

		internal ODataQueryExpression Update(ODataExpression filter = null, IEnumerable<ODataSortKeyExpression> orderBy = null, int? top = -1, int? skip = null, string format = null, ODataInlineCountOption? inlineCount = null, IEnumerable<ODataSelectColumnExpression> select = null)
		{
			return Query(
				filter: filter ?? this.Filter,
				orderBy: orderBy.NullSafe(ob => ob.ToArray(), ifNullReturn: this.OrderBy),
				top: top == -1 ? this.Top : top,
				skip: skip ?? this.Skip,
				format: format ?? this.Format,
				inlineCount: inlineCount ?? this.InlineCount,
				select: select.NullSafe(sc => sc.ToArray(), ifNullReturn: this.Select)
			);
		}

		private static void AppendParam(StringBuilder builder, string paramName, string value)
		{
			if (builder[builder.Length - 1] != '?')
			{
				builder.Append('&');
			}
			builder.Append(paramName)
				.Append('=')
				.Append(WebUtility.UrlEncode(value));
		}
	}
}
