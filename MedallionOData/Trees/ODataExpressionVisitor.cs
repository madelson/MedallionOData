using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
	internal class ODataExpressionVisitor
	{
		public virtual void Visit(ODataExpression node)
		{
			if (node == null)
			{
				return;
			}
			switch (node.Kind)
			{
				case ODataExpressionKind.Convert:
					this.VisitConvert((ODataConvertExpression)node);
					break;
				case ODataExpressionKind.Constant:
					this.VisitConstant((ODataConstantExpression)node);
					break;
				case ODataExpressionKind.Call:
					this.VisitCall((ODataCallExpression)node);
					break;
				case ODataExpressionKind.BinaryOp:
					this.VisitBinaryOp((ODataBinaryOpExpression)node);
					break;
				case ODataExpressionKind.UnaryOp:
					this.VisitUnaryOp((ODataUnaryOpExpression)node);
					break;
				case ODataExpressionKind.MemberAccess:
					this.VisitMemberAccess((ODataMemberAccessExpression)node);
					break;
				case ODataExpressionKind.Query:
					this.VisitQuery((ODataQueryExpression)node);
					break;
				case ODataExpressionKind.SelectColumn:
					this.VisitSelectColumn((ODataSelectColumnExpression)node);
					break;
				case ODataExpressionKind.SortKey:
					this.VisitSortKey((ODataSortKeyExpression)node);
					break;
				default:
					throw new InvalidOperationException("Unexpected expression kind " + node.Kind);
			}
		}

		protected virtual void VisitBinaryOp(ODataBinaryOpExpression node)
		{
			this.Visit(node.Left);
			this.Visit(node.Right);
		}

		protected virtual void VisitUnaryOp(ODataUnaryOpExpression node)
		{
			this.Visit(node.Operand);
		}

		protected virtual void VisitMemberAccess(ODataMemberAccessExpression node)
		{
			this.Visit(node.Expression);
		}

		protected virtual void VisitConstant(ODataConstantExpression node)
		{
		}

		protected virtual void VisitCall(ODataCallExpression node)
		{
			foreach (ODataExpression argument in node.Arguments) 
			{
				this.Visit(argument);
			}
		}

		protected virtual void VisitConvert(ODataConvertExpression node)
		{
			this.Visit(node.Expression);
		}

		protected virtual void VisitQuery(ODataQueryExpression node)
		{
			this.Visit(node.Filter);
			foreach (var key in node.OrderBy)
			{
				this.Visit(key);
			}
			foreach (var col in node.Select)
			{
				this.Visit(col);
			}
		}

		protected virtual void VisitSortKey(ODataSortKeyExpression node)
		{
			this.Visit(node.Expression);
		}

		protected virtual void VisitSelectColumn(ODataSelectColumnExpression node)
		{
			this.Visit(node.Expression);
		}
	}
}
