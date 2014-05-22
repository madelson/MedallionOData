using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    internal static class QueryOperatorCanonicalizer
    {
        public static MethodCallExpression Canonicalize(MethodCallExpression call, out bool changedAllToAny)
        {
            changedAllToAny = false;

            if (call.Method.DeclaringType != typeof(Queryable))
            {
                return call;
            }

            // simplify methods with predicates and selectors
            if (call.Arguments.Count == 2)
            {
                switch (call.Method.Name) 
                {
                    // methods with predicates
                    case "Any":
                    case "First":
                    case "FirstOrDefault":
                    case "Single":
                    case "SingleOrDefault":
                    case "Count":
                    case "LongCount":
                        return Apply(call.Method.Name, Apply("Where", call.Arguments[0], call.Arguments[1])); 
                    case "All":
                        changedAllToAny = true;
                        var predicate = (LambdaExpression)call.Arguments[1].UnQuote();
                        return Apply("Any", Apply("Where", call.Arguments[0], Expression.Quote(Expression.Lambda(Expression.Not(predicate.Body), predicate.Parameters))));
                    // methods with selectors
                    case "Min":
                    case "Max":
                        return Apply(call.Method.Name, Apply("Select", call.Arguments[0], call.Arguments[1]));
                    case "Contains":
                        var queryElementType = GetElementType(call);
                        Throw<ODataCompileException>.If(
                            queryElementType.ToODataExpressionType() == ODataExpressionType.Complex,
                            "Query operator Contains is only supported for complex types"
                        );

                        // Contains(foo) => Any(x => x == foo)
                        var parameter = Expression.Parameter(queryElementType);
                        var any = Apply("Any", call.Arguments[0], Expression.Quote(Expression.Lambda(Expression.Equal(parameter, call.Arguments[1]), parameter)));
                        return Canonicalize(any, out changedAllToAny);
                    default:
                        return call;
                }
            }

            switch (call.Method.Name)
            {
                // translate min/max to OrderBy().Take(1).First()
                case "Min":
                case "Max":
                    var elementType = GetElementType(call);
                    var parameter = Expression.Parameter(elementType);
                    // min and max filter nulls, so add a filter if the element type is nullable
                    var withoutNulls = elementType.CanBeNull()
                        ? Apply("Where", call.Arguments[0], Expression.Lambda(Expression.NotEqual(parameter, Expression.Constant(null, elementType)), parameter))
                        : call.Arguments[0];
                    var sorted = Apply(
                        "OrderBy" + (call.Method.Name == "Max" ? "Descending" : string.Empty),
                        withoutNulls,
                        Expression.Quote(Expression.Lambda(parameter, parameter)) // x => x
                    );
                    var limited = Apply("Take", sorted, Expression.Constant(1));
                    var first = Apply("First", limited);
                    return first;
                default:
                    return call;
            }
        }

        private static Type GetElementType(MethodCallExpression call)
        {
            return call.Arguments[0].Type.GetGenericArguments(typeof(IQueryable<>)).Single();
        }

        private static MethodCallExpression Apply(string methodName, Expression source, Expression argument = null)
        {
            var result = Expression.Call(
                type: typeof(Queryable),
                methodName: methodName,
                typeArguments: methodName == "Select" || methodName.StartsWith("OrderBy", StringComparison.Ordinal)
                    ? source.Type.GetGenericArguments(typeof(IQueryable<>))
                        .Concat(new[] { ((LambdaExpression)LinqHelpers.UnQuote(argument)).ReturnType })
                        .ToArray()
                    : source.Type.GetGenericArguments(typeof(IQueryable<>)),
                arguments: argument != null
                    ? new[] { source, argument }
                    : new[] { source }
            );
            return result;
        }
    }
}
