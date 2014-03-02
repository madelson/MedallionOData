using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;

namespace Medallion.OData.Service
{
	internal static class ODataQueryFilter
	{
		public static IQueryable<TElement> Apply<TElement>(IQueryable<TElement> query, ODataQueryExpression oDataQuery, out IQueryable<TElement> inlineCountQuery)
		{
			var finalQuery = query;

			if (oDataQuery.Filter != null)
			{
				var parameter = Expression.Parameter(typeof(TElement));
				var predicate = Expression.Lambda<Func<TElement, bool>>(Translate(parameter, oDataQuery.Filter), parameter);
				finalQuery = finalQuery.Where(predicate);
			}

            inlineCountQuery = finalQuery;

			for (var i = 0; i < oDataQuery.OrderBy.Count; ++i)
			{
				finalQuery = ApplyOrderBy(finalQuery, oDataQuery.OrderBy[i], isPrimarySort: i == 0);
			}

			if (oDataQuery.Skip > 0)
			{
				finalQuery = finalQuery.Skip(oDataQuery.Skip);
			}

			if (oDataQuery.Top.HasValue)
			{
				finalQuery = finalQuery.Take(oDataQuery.Top.Value);
			}

			return finalQuery;
		}

		private static IQueryable<TElement> ApplyOrderBy<TElement>(IQueryable<TElement> query, ODataSortKeyExpression sortKey, bool isPrimarySort)
		{
			var parameter = Expression.Parameter(typeof(TElement));
			var translated = Translate(parameter, sortKey.Expression);
			var lambda = Expression.Lambda(translated, parameter);

			var methodName = (isPrimarySort ? "OrderBy" : "ThenBy") + (sortKey.Direction == ODataSortDirection.Descending ? "Descending" : string.Empty);
			MethodInfo method;
			switch (methodName)
			{
				case "OrderBy":
					method = Helpers.GetMethod((IQueryable<object> q) => q.OrderBy(o => o));
					break;
				case "OrderByDescending":
                    method = Helpers.GetMethod((IQueryable<object> q) => q.OrderByDescending(o => o));
					break;
				case "ThenBy":
                    method = Helpers.GetMethod((IOrderedQueryable<object> q) => q.ThenBy(o => o));
					break;
				case "ThenByDescending":
                    method = Helpers.GetMethod((IOrderedQueryable<object> q) => q.ThenByDescending(o => o));
					break;
				default:
					throw Throw.UnexpectedCase(methodName);
			}

			var result = method.GetGenericMethodDefinition()
				.MakeGenericMethod(lambda.Type.GetGenericArguments(typeof(Func<,>)))
				.Invoke(null, new object[] { query, lambda });
			return (IQueryable<TElement>)result;
		}

		private static Expression Translate(ParameterExpression parameter, ODataExpression expression)
		{
			switch (expression.Kind)
			{
				case ODataExpressionKind.BinaryOp:
					var binaryOp = (ODataBinaryOpExpression)expression;
					var originalLeft = Translate(parameter, binaryOp.Left);
					var originalRight = Translate(parameter, binaryOp.Right);
					var left = originalLeft.NegotiateNullabilityWith(originalRight);
					var right = originalRight.NegotiateNullabilityWith(originalLeft);
					switch (binaryOp.Operator)
					{
						case ODataBinaryOp.Or:
							return Expression.Or(left, right);
						case ODataBinaryOp.And:
							return Expression.And(left, right);
						case ODataBinaryOp.Add:
							return Expression.Add(left, right);
						case ODataBinaryOp.Subtract:
							return Expression.Subtract(left, right);
						case ODataBinaryOp.Multiply:
							return Expression.Multiply(left, right);
						case ODataBinaryOp.Divide:
							return Expression.Divide(left, right);
						case ODataBinaryOp.Modulo:
							return Expression.Modulo(left, right);
						case ODataBinaryOp.Equal:
							return Expression.Equal(left, right);
						case ODataBinaryOp.NotEqual:
							return Expression.NotEqual(left, right);
						case ODataBinaryOp.GreaterThan:
							return Expression.GreaterThan(left, right);
						case ODataBinaryOp.GreaterThanOrEqual:
							return Expression.GreaterThanOrEqual(left, right);
						case ODataBinaryOp.LessThan:
							return Expression.LessThan(left, right);
						case ODataBinaryOp.LessThanOrEqual:
							return Expression.LessThanOrEqual(left, right);
						default:
							throw Throw.UnexpectedCase(binaryOp.Operator);
					}
				case ODataExpressionKind.Call:
					var call = (ODataCallExpression)expression;
					var arguments = call.Arguments.Select(a => Translate(parameter, a))
						.ToArray();
					switch (call.Function)
					{
						// TODO NullReferenceExceptions
						// TODO real dates
						case ODataFunction.Cast:
							return Expression.Convert(arguments[0], (Type)((ConstantExpression)arguments[1]).Value);
						case ODataFunction.Ceiling:
							return Expression.Call(typeof(Math), "Ceiling", Type.EmptyTypes, arguments.Single());
						case ODataFunction.Floor:
							return Expression.Call(typeof(Math), "Floor", Type.EmptyTypes, arguments.Single());
						case ODataFunction.Concat:
							return Expression.Call(Helpers.GetMethod(() => string.Concat(default(string), default(string))), arguments);
						case ODataFunction.Length:
							return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((string s) => s.Length));
						case ODataFunction.EndsWith:
							return Expression.Call(arguments[0], "EndsWith", Type.EmptyTypes, arguments[1]);
						case ODataFunction.StartsWith:
							return Expression.Call(arguments[0], "StartsWith", Type.EmptyTypes, arguments[1]);

                        // MA: I'm not 100% sure that EF supports these properties because they also have the SqlFunctions. However, if they
                        // don't we can always apply a custom post-translator to replace them
						case ODataFunction.Day:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Day));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("d"), arguments.Single());
						case ODataFunction.Hour:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Hour));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("h"), arguments.Single());
						case ODataFunction.Month:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Month));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("m"), arguments.Single());
						case ODataFunction.Minute:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Minute));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("mi"), arguments.Single());
						case ODataFunction.Second:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Second));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("s"), arguments.Single());
						case ODataFunction.Year:
                            return Expression.MakeMemberAccess(arguments.Single(), Helpers.GetProperty((DateTime dt) => dt.Year));
							//return Expression.Call(typeof(SqlFunctions), "DatePart", Type.EmptyTypes, Expression.Constant("yy"), arguments.Single());

						case ODataFunction.IndexOf:
							return Expression.Call(arguments[0], "IndexOf", Type.EmptyTypes, arguments[1]);
						case ODataFunction.IsOf:
							return arguments.Length == 1
								? Expression.TypeIs(parameter, (Type)((ConstantExpression)arguments.Single()).Value)
								: Expression.TypeIs(arguments[0], (Type)((ConstantExpression)arguments[1]).Value);
						case ODataFunction.Replace:
							return Expression.Call(arguments[0], "Replace", Type.EmptyTypes, arguments[1], arguments[2]);
						case ODataFunction.Round:
							return Expression.Call(typeof(Math), "Round", Type.EmptyTypes, arguments.Single());
						case ODataFunction.Substring:
							return arguments.Length == 2
								? Expression.Call(arguments[0], "Substring", Type.EmptyTypes, arguments[1])
								: Expression.Call(arguments[0], "Substring", Type.EmptyTypes, arguments[1], arguments[2]);
						case ODataFunction.SubstringOf:
							// http://services.odata.org/Northwind/Northwind.svc/Customers?$filter=substringof('Alfreds', CompanyName) eq true&$format=application/json
							// returns an entry with "CompanyName":"Alfreds Futterkiste". Thus, this is interpreted as "is the first argument a substring of the second"
							// or second.Contains(first)
							return Expression.Call(arguments[1], "Contains", Type.EmptyTypes, arguments[0]);
						case ODataFunction.ToLower:
							return Expression.Call(arguments.Single(), "ToLower", Type.EmptyTypes);
						case ODataFunction.ToUpper:
							return Expression.Call(arguments.Single(), "ToUpper", Type.EmptyTypes);
						case ODataFunction.Trim:
							return Expression.Call(arguments.Single(), "Trim", Type.EmptyTypes);
						default:
							throw Throw.UnexpectedCase(call.Function);
					}
				case ODataExpressionKind.Constant:
					// note that we don't pass a type here but instead rely on the value being the right type (guaranteed by the static factory)
					// that also allows this to work better with nulls
					return Expression.Constant(((ODataConstantExpression)expression).Value);
				case ODataExpressionKind.Convert:
					var toConvert = Translate(parameter, ((ODataConvertExpression)expression).Expression);
					var convertToType = expression.Type.ToClrType();
					return Expression.Convert(toConvert, toConvert.Type.CanBeNull() && !convertToType.CanBeNull() ? typeof(Nullable<>).MakeGenericType(convertToType) : convertToType);
				case ODataExpressionKind.MemberAccess:
					var memberAccess = (ODataMemberAccessExpression)expression;
					return Expression.MakeMemberAccess(memberAccess.Expression != null ? Translate(parameter, memberAccess.Expression) : parameter, memberAccess.Member);
				case ODataExpressionKind.UnaryOp:
					var unaryOp = (ODataUnaryOpExpression)expression;
					switch (unaryOp.Operator)
					{
						case ODataUnaryOp.Not:
							return Expression.Not(Translate(parameter, unaryOp.Operand));
						default:
							throw Throw.UnexpectedCase(unaryOp);
					}
				default:
					throw Throw.UnexpectedCase(expression.Kind, "Cannot translate " + expression.Kind + " to LINQ");
			}
		}

		private static Expression NegotiateNullabilityWith(this Expression @this, Expression that)
		{
			return @this.Type == Nullable.GetUnderlyingType(that.Type)
				? Expression.Convert(@this, that.Type)
				: @this;
		}
	}
}
