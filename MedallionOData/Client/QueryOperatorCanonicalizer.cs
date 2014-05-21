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

            if (call.Method.DeclaringType != typeof(Queryable)
                || call.Arguments.Count != 2)
            {
                return call;
            }

            switch (call.Method.Name) 
            {
                // methods with predicates
                case "Any":
                case "First":
                case "FirstOrDefault":
                case "Single":
                case "SingleOrDefault":
                case "Count":
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
                    var queryElementType = call.Arguments[0].Type.GetGenericArguments(typeof(IQueryable<>)).Single();
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

        private static MethodCallExpression Apply(string methodName, Expression source, Expression argument = null)
        {
            var result = Expression.Call(
                type: typeof(Queryable),
                methodName: methodName,
                typeArguments: methodName == "Select"
                    ? source.Type.GetGenericArguments(typeof(IQueryable<>))
                        .Concat(new[] { ((LambdaExpression)argument).ReturnType })
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
