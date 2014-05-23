using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    internal interface IODataQueryable<TElement>
    {
        Task<IODataResult<TElement>> ExecuteQueryAsync(ODataQueryOptions options = null);
        Task<TResult> ExecuteAsync<TResult>(Expression<Func<IQueryable<TElement>, TResult>> executeExpression);
    }

    public interface IODataResult<TElement>
    {
        /// <summary>
        /// The results of the query
        /// </summary>
        IReadOnlyList<TElement> Results { get; }            
        /// <summary>
        /// The total number of items in the query IGNORING pagination. This value will be populated only if the
        /// <see cref="ODataQueryOptions.InlineCount"/> option is specified
        /// </summary>
        int? TotalCount { get; }
    }

    public static class ODataQueryable
    {
        public static Task<IODataResult<TElement>> ExecuteQueryAsync<TElement>(this IQueryable<TElement> @this, ODataQueryOptions options = null)
        {
            Throw.IfNull(@this, "this");
            var oDataQuery = @this as IODataQueryable<TElement>;
            Throw<ArgumentException>.If(oDataQuery == null, () => "this: must implement " + typeof(IODataResult<TElement>));
            return oDataQuery.ExecuteQueryAsync(options);
        }

        public static IODataResult<TElement> ExecuteQuery<TElement>(this IQueryable<TElement> @this, ODataQueryOptions options = null)
        {
            return @this.ExecuteQueryAsync(options).GetResultWithOriginalException();
        }

        public static Task<TResult> ExecuteAsync<TElement, TResult>(this IQueryable<TElement> @this, Expression<Func<IQueryable<TElement>, TResult>> executeExpression)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(executeExpression, "executeExpression");
            var oDataQuery = @this as IODataQueryable<TElement>;
            Throw<ArgumentException>.If(oDataQuery == null, () => "this: must implement " + typeof(IODataResult<TElement>));
            return oDataQuery.ExecuteAsync(executeExpression);
        }
    }
}
