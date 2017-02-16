using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;
using System.Runtime.Serialization;

namespace Medallion.OData.Client
{
	internal partial class LinqToODataTranslator
	{
        internal delegate object ResultTranslator(IEnumerable values, int? inlineCount);

		private bool _isInsideQuery;
		private IQueryable _rootQuery;
        private ResultTranslator _resultTranslator;
		private MemberAndParameterTranslator _memberAndParameterTranslator;

		public ODataExpression Translate(Expression linq, out IQueryable rootQuery, out ResultTranslator resultTranslator)
		{
			this._isInsideQuery = false;
			this._rootQuery = null;
			this._resultTranslator = null;
			this._memberAndParameterTranslator = new MemberAndParameterTranslator(this);

            // normalize away ODataRow constructs
            var normalized = ODataEntity.Normalize(linq);

			var translated = this.TranslateInternal(normalized);

			if (translated.Kind == ODataExpressionKind.Query)
			{			
				var referencedPaths = this._memberAndParameterTranslator.GetReferencedMemberPathsInFinalProjection();
				if (referencedPaths != null)
				{
					var selectColumns = referencedPaths.Select(p => p.Aggregate(default(ODataMemberAccessExpression), (e, m) => ODataExpression.MemberAccess(e, (PropertyInfo)m)))
						.Select(ma => ODataExpression.SelectColumn(ma, allColumns: ma.Type == ODataExpressionType.Complex));
					translated = ((ODataQueryExpression)translated).Update(select: selectColumns);
				}				
			}

			rootQuery = this._rootQuery;

			var projection = this._memberAndParameterTranslator.GetFinalProjection();
			var finalTranslator = this._resultTranslator ?? ((values, count) => values);
			if (projection != null)
			{
				var selectMethod = Helpers.GetMethod((IEnumerable<object> e) => e.Select(o => o))
					.GetGenericMethodDefinition()
					.MakeGenericMethod(projection.Type.GetGenericArguments(typeof(Func<,>)));

                // restores any ODataRow constructs that were normalized away, since we need to be able to compile and run the projection
                // (i. e. fake ODataRow property accesses don't run when compiled)
                var denormalizedProjection = (LambdaExpression)ODataEntity.Denormalize(projection);
				
                Func<object, object> queryTranslator = enumerable => selectMethod.Invoke(null, new[] { enumerable, denormalizedProjection.Compile() });
				resultTranslator = (values, count) => finalTranslator((IEnumerable)queryTranslator(values), count);
			}
			else
			{
				resultTranslator = finalTranslator;
			}
			return translated;
		}

		private ODataExpression TranslateInternal(Expression linq)
		{
			if (linq == null)
			{
				return null;
			}

			// captured parameters
			object value;
			if (TryGetValueFast(linq, out value))
			{
				// special case the handling of queryable constants, since these can be the "root"
				// of the query expression tree. We check for isInsideQuery since we don't allow multiple
				// roots. An example of a multiply-rooted tree would be: q.Where(x => q.Any(xx => xx.A < x.A))
				if (!this._isInsideQuery && typeof(IQueryable).GetTypeInfo().IsAssignableFrom(linq.Type.GetTypeInfo()))
				{
					this._rootQuery = (IQueryable)value;
					return ODataExpression.Query();
				}
				return ODataExpression.Constant(value, linq.Type);
			}

			switch (linq.NodeType)
			{
				case ExpressionType.OrElse:
					return this.TranslateBinary(linq, ODataBinaryOp.Or);
				case ExpressionType.AndAlso:
					return this.TranslateBinary(linq, ODataBinaryOp.And);
				case ExpressionType.Add:
					if (linq.Type == typeof(string))
					{
						// special case adding strings, since that's the same as Concat
						var binary = (BinaryExpression)linq;
						return this.TranslateInternal(Expression.Call(Helpers.GetMethod(() => string.Concat(default(string), default(string))), binary.Left, binary.Right));
					}
					return this.TranslateBinary(linq, ODataBinaryOp.Add);
				case ExpressionType.Subtract:
					return this.TranslateBinary(linq, ODataBinaryOp.Subtract);
				case ExpressionType.Multiply:
					return this.TranslateBinary(linq, ODataBinaryOp.Multiply);
                case ExpressionType.Divide:
                    return this.TranslateBinary(linq, ODataBinaryOp.Divide);
				case ExpressionType.Modulo:
					return this.TranslateBinary(linq, ODataBinaryOp.Modulo);
				case ExpressionType.Equal:
					return this.TranslateBinary(linq, ODataBinaryOp.Equal);
				case ExpressionType.NotEqual:
					return this.TranslateBinary(linq, ODataBinaryOp.NotEqual);
				case ExpressionType.LessThan:
					return this.TranslateBinary(linq, ODataBinaryOp.LessThan);
				case ExpressionType.LessThanOrEqual:
					return this.TranslateBinary(linq, ODataBinaryOp.LessThanOrEqual);
				case ExpressionType.GreaterThan:
					return this.TranslateBinary(linq, ODataBinaryOp.GreaterThan);
				case ExpressionType.GreaterThanOrEqual:
					return this.TranslateBinary(linq, ODataBinaryOp.GreaterThanOrEqual);
				case ExpressionType.Not:
					return ODataExpression.UnaryOp(this.TranslateInternal(((UnaryExpression)linq).Operand), ODataUnaryOp.Not);
                case ExpressionType.Negate:
                    // we translate -a.B as (-1 * a.B) for numeric types
                    var negated = ((UnaryExpression)linq).Operand;
                    var underlyingNegatedType = Nullable.GetUnderlyingType(negated.Type) ?? negated.Type;
                    if (!underlyingNegatedType.IsNumeric())
                    {
                        throw new ODataCompileException("Expression '" + linq + "' could not be translated to OData: only negation of numeric types is supported");
                    }
                    var multiplyNegate = Expression.Multiply(
                        Expression.Constant(Convert.ChangeType(-1, underlyingNegatedType), negated.Type),
                        negated
                    );
                    return this.TranslateInternal(multiplyNegate);
				case ExpressionType.MemberAccess:
					return this._memberAndParameterTranslator.TranslateMemberAccess((MemberExpression)linq);
				case ExpressionType.Convert:
				case ExpressionType.ConvertChecked:
				case ExpressionType.TypeAs:
					return ODataExpression.Convert(this.TranslateInternal(((UnaryExpression)linq).Operand), linq.Type);
				case ExpressionType.Parameter:
					return this._memberAndParameterTranslator.TranslateParameter((ParameterExpression)linq);
				case ExpressionType.TypeIs:
					var typeBinary = (TypeBinaryExpression)linq;
					var isArg = this.TranslateInternal(typeBinary.Expression);
					var isTypeConstant = ODataExpression.Constant(typeBinary.TypeOperand);
					return isArg != null
						? ODataExpression.Call(ODataFunction.IsOf, new[] { isArg, isTypeConstant })
						: ODataExpression.Call(ODataFunction.IsOf, new[] { isTypeConstant });
				case ExpressionType.Call:
					return this.TranslateCall((MethodCallExpression)linq);
				case ExpressionType.Quote:
					var quoted = (LambdaExpression)((UnaryExpression)linq).Operand;
					if (!this._isInsideQuery)
					{
						throw new ODataCompileException("Unexpected placement for lambda expression " + linq);
					}
					return this.TranslateInternal(quoted.Body);
				default:
					throw new ODataCompileException("Expression '" + linq + "' of type " + linq.NodeType + " could not be translated to OData");
			}
		}

		private ODataExpression TranslateBinary(Expression linq, ODataBinaryOp op)
		{
			var binary = (BinaryExpression)linq;
			return ODataExpression.BinaryOp(this.TranslateInternal(binary.Left), op, this.TranslateInternal(binary.Right));
		}

		private ODataExpression TranslateCall(MethodCallExpression call)
		{
			// query operators
			if (call.Method.DeclaringType == typeof(Queryable))
			{
				if (this._isInsideQuery)
				{
					throw new ODataCompileException("OData does not support nested query structures!");
				}

                // normalize overloads of query operators
                bool changedAllToAny;
                var normalized = QueryOperatorCanonicalizer.Canonicalize(call, changedAllToAny: out changedAllToAny);
                if (normalized != call)
                {
                    // TODO FUTURE better exception message here since expression being translated won't match?
                    var result = this.TranslateInternal(normalized);
                    if (changedAllToAny)
                    {
                        var innerResultTranslator = this._resultTranslator;
                        Throw<InvalidOperationException>.If(innerResultTranslator == null, "Sanity check: expected non-null result translator");
                        this._resultTranslator = (values, count) => !((bool)innerResultTranslator(values, count));
                    }
                    return result;
                }

				// handle "normal" query methods
				
				// translate the source query. If the source is a constant, that's the "root" of the query tree
				// e. g. that might be the OData equivalent of a DbSet or ObjectQuery constant
				var source = (ODataQueryExpression)this.TranslateInternal(call.Arguments[0]);
				switch (call.Method.Name)
				{
					case "Where":
						// not the index version
						if (call.Arguments[1].Type.IsGenericOfType(typeof(Func<,,>)))
						{
							goto default;
						}

						var predicate = this.TranslateInsideQuery(call.Arguments[1]);
						if (source.OrderBy.Count > 0 || source.Top.HasValue || source.Skip > 0)
						{
							throw new ODataCompileException("Cannot apply a filter after applying an OrderBy, Take, or Skip operation");
						}
						return source.Update(filter: source.Filter != null ? ODataExpression.BinaryOp(source.Filter, ODataBinaryOp.And, predicate) : predicate);
					case "OrderBy":
					case "OrderByDescending":
					case "ThenBy":
					case "ThenByDescending":
						if (call.Arguments.Count != 2)
						{
							goto default;
						}

						var sortKeyExpression = this.TranslateInsideQuery(call.Arguments[1]);
						var sortKey = ODataExpression.SortKey(sortKeyExpression, descending: call.Method.Name.EndsWith("Descending"));
						if (source.Top.HasValue || source.Skip > 0)
						{
							throw new ODataCompileException("Cannot apply a sort after applying Take or Skip operations");
						}
						return source.Update(
							orderBy: call.Method.Name.StartsWith("Then")
								? source.OrderBy.Concat(sortKey.Enumerate())
								: sortKey.Enumerate()
						);
					case "Skip":
						object skip;
						Throw<InvalidOperationException>.If(!TryGetValueFast(call.Arguments[1], out skip), "Could not get value");

						if (source.Top.HasValue)
						{
							throw new ODataCompileException("Cannot apply a skip after applying a Take operation");
						}
						return source.Update(skip: source.Skip + (int)skip); // not right
					case "Take":
						object take;
                        Throw<InvalidOperationException>.If(!TryGetValueFast(call.Arguments[1], out take), "Could not get value");
                        return Take(source, (int)take);
					case "Select":
						/*
						 * MA: select is tricky in OData, since it doesn't just conform to the OData $select system query option.
						 * Instead, you could have an intermediate select in your query, and it would be nice to be able to support that.
						 * In that case, OData still forces us to select original columns, but we can store away the projection expressions
						 * to re-apply later. We also need to make sure to store how to map each projected property, so that we can inline these
						 * projections.
						 * 
						 * For example, imagine you have the query q.Select(x => new { a = x.B + 2 }).Where(t => t.a > 5).
						 * In OData, this would translate to "$filter=B + 2 > 5&$select=B". We can then do the final projection to the anonymous
						 * type in memory on the client side
						 */

						// we don't support select with index
						if (!call.Arguments[1].Type.GetGenericArguments(typeof(Expression<>)).Single().IsGenericOfType(typeof(Func<,>)))
						{
							goto default;
						}

						// unquote and extract the lambda
						var projection = ((LambdaExpression)((UnaryExpression)call.Arguments[1]).Operand);

						// register the projection
						this._isInsideQuery = true;
						this._memberAndParameterTranslator.RegisterProjection(projection);
						this._isInsideQuery = false;

						// return the source, since the projection doesn't actually affect the returned expression
						// until the very end when we can use it to determine which columns to $select
						return source;
                    case "Any":
                        this._resultTranslator = MakeTranslator(call.Arguments[0], e => e.Any());
                        return Take(source, 1);
                    case "Count":
                        this._resultTranslator = (values, count) => ComputeCount(count: count.Value, skip: source.Skip, top: source.Top);
                        // top 0 is so that we don't have any wasted payload of data that we won't look at
                        return source.Update(inlineCount: ODataInlineCountOption.AllPages, top: 0);
                    case "LongCount":
                        this._resultTranslator = (values, count) => (long)ComputeCount(count: count.Value, skip: source.Skip, top: source.Top);
                        // top 0 is so that we don't have any wasted payload of data that we won't look at
                        return source.Update(inlineCount: ODataInlineCountOption.AllPages, top: 0);
                    case "First":
                        this._resultTranslator = MakeTranslator(call.Arguments[0], e => e.First());
                        return Take(source, 1);
                    case "FirstOrDefault":
                        this._resultTranslator = MakeTranslator(call.Arguments[0], e => e.FirstOrDefault());
                        return Take(source, 1);
                    case "Single":
                        this._resultTranslator = MakeTranslator(call.Arguments[0], e => e.Single());;
                        return Take(source, 2);
                    case "SingleOrDefault":
                        this._resultTranslator = MakeTranslator(call.Arguments[0], e => e.SingleOrDefault());;
                        return Take(source, 2);
					default:
						throw new ODataCompileException("Query operator " + call.Method + " is not supported in OData");
				}
			}

			// other OData methods

			// Enumerable/Collection contains (e. g. "IN"). This gets handled before the odata functions because
			// the thisExpression can't be translated normally. Since Contains() is declared on a bunch of different collection
			// types, we basically check that (1) there are either 2 arguments (static method) or an instance + 1 argument
			// (2) that the container argument/instance is an in-memory "constant", (3) that the collection object has an IEnumerable<T>
			// element type, and (4) that the test argument is of that element type
			// Finally, we translate the IN clause to a set of ORs
			object enumerable;
			Type elementType;
			if (call.Method.Name == "Contains" 
				&& call.Arguments.Count + Convert.ToInt32(!call.Method.IsStatic) == 2
				&& TryGetValueFast(call.Object ?? call.Arguments[0], out enumerable)
				&& enumerable != null
				&& (elementType = enumerable.GetType().GetGenericArguments(typeof(IEnumerable<>)).SingleOrDefault()) != null
				&& call.Arguments.Last().Type == elementType)
			{
				var testExpression = this.TranslateInternal(call.Arguments.Last());
				var equalsElementExpressions = ((IEnumerable)enumerable).Cast<object>()
					.Select(o => ODataExpression.Constant(o, elementType))
					.Select(c => ODataExpression.BinaryOp(testExpression, ODataBinaryOp.Equal, c))
					.ToArray();
				var equivalentOrExpression = equalsElementExpressions.Length == 0
					? ODataExpression.Constant(true).As<ODataExpression>()
					: equalsElementExpressions.Aggregate((e1, e2) => ODataExpression.BinaryOp(e1, ODataBinaryOp.Or, e2));
				return equivalentOrExpression;
			}

			// ODataFunctions
			var thisExpression = this.TranslateInternal(call.Object);
			var translatedArgs = call.Arguments.Select(this.TranslateInternal);

			// string functions
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "Substring")
			{
				return ODataExpression.Call(ODataFunction.Substring, thisExpression.Enumerate().Concat(translatedArgs));
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "Replace" && call.Arguments[0].Type == typeof(string))
			{
				// for now, we don't support the char replace overload			
				return ODataExpression.Call(ODataFunction.Replace, thisExpression.Enumerate().Concat(translatedArgs));
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "Concat" && call.Arguments.All(a => a.Type == typeof(string)))
			{
				// we support only string concats, but with any fixed number of parameters
				return translatedArgs.Aggregate((s1, s2) => ODataExpression.Call(ODataFunction.Concat, new[] { s1, s2 }));
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "StartsWith" && call.Arguments.Count == 1)
			{
				return ODataExpression.Call(ODataFunction.StartsWith, new[] { thisExpression, translatedArgs.Single() });
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "EndsWith" && call.Arguments.Count == 1)
			{
				return ODataExpression.Call(ODataFunction.EndsWith, new[] { thisExpression, translatedArgs.Single() });
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "IndexOf" && call.Arguments.Count == 1 && call.Arguments[0].Type == typeof(string))
			{
				return ODataExpression.Call(ODataFunction.IndexOf, new[] { thisExpression, translatedArgs.Single() });
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "Contains" && call.Arguments.Count == 1 && call.Arguments[0].Type == typeof(string))
			{
				// note: we reverse the args here because A.SubstringOf(B) is equivalent to B.Contains(A)
				return ODataExpression.Call(ODataFunction.SubstringOf, new[] { translatedArgs.Single(), thisExpression });
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "ToLower" && call.Arguments.Count == 0)
			{
				return ODataExpression.Call(ODataFunction.ToLower, thisExpression.Enumerate());
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "ToUpper" && call.Arguments.Count == 0)
			{
				return ODataExpression.Call(ODataFunction.ToUpper, thisExpression.Enumerate());
			}
			if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "Trim" && call.Arguments.Count == 0)
			{
				return ODataExpression.Call(ODataFunction.Trim, thisExpression.Enumerate());
			}

			// math functions
			if (call.Method.DeclaringType == typeof(Math) && call.Method.Name == "Ceiling")
			{
				return ODataExpression.Call(ODataFunction.Ceiling, translatedArgs);
			}
			if (call.Method.DeclaringType == typeof(Math) && call.Method.Name == "Floor")
			{
				return ODataExpression.Call(ODataFunction.Floor, translatedArgs);
			}
			if (call.Method.DeclaringType == typeof(Math) && call.Method.Name == "Round" && call.Arguments.Count == 1)
			{
				return ODataExpression.Call(ODataFunction.Round, translatedArgs);
			}

			throw new ODataCompileException("Method " + call.Method + " could not be translated to OData");
		}

		private ODataExpression TranslateInsideQuery(Expression expression)
		{
			this._isInsideQuery = true;
			var result = this.TranslateInternal(expression);
			this._isInsideQuery = false;
			return result;
		}

        private ResultTranslator MakeTranslator(Expression queryExpression, Expression<Action<IEnumerable<object>>> translatorMethod)
        {
            var elementType = queryExpression.Type.GetGenericArguments(typeof(IQueryable<>)).Single();
            var method = Helpers.GetMethod(translatorMethod)
                .GetGenericMethodDefinition()
                .MakeGenericMethod(elementType);
            return (values, count) => method.InvokeWithOriginalException(null, new object[] { values });
        }

        private static ODataQueryExpression Take(ODataQueryExpression query, int take)
        {
            return query.Update(top: Math.Min(query.Top ?? int.MaxValue, take));
        }

        private static int ComputeCount(int count, int skip, int? top)
        {
            var applySkip = Math.Max(count - skip, 0);
            if (top.HasValue)
            {
                var applyTop = Math.Min(applySkip, top.Value);
                return applyTop;
            }
            return applySkip;
        }

		private static bool TryGetValueFast(Expression expression, out object value)
		{
			return expression.TryGetValue(LinqHelpers.GetValueOptions.ConstantsFieldsAndProperties, out value);
		}
	}

    /// <summary>
    /// Represents an error in compiling an <see cref="IQueryable"/> to OData
    /// </summary>
#if !NETCORE
    [Serializable]
#endif
    public sealed class ODataCompileException : Exception
	{
        /// <summary>
        /// Constructs an exception with a default message
        /// </summary>
        public ODataCompileException() : this("compilation to OData failed") { }

        /// <summary>
        /// Constructs an exception from a message
        /// </summary>
		public ODataCompileException(string message)
            : base(message) { }

        /// <summary>
        /// Constructs an exception from a message and inner exception
        /// </summary>
        public ODataCompileException(string message, Exception innerException)
            : base(message, innerException) { }

        internal ODataCompileException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
	}
}
