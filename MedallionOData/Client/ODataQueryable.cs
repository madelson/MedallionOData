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

    /// <summary>
    /// Represents the result of an OData query
    /// </summary>
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

    /// <summary>
    /// Provides query operators for OData queries
    /// </summary>
    public static class ODataQueryable
    {
        /// <summary>
        /// Asynchronously executes the given OData query
        /// </summary>
        public static Task<IODataResult<TElement>> ExecuteQueryAsync<TElement>(this IQueryable<TElement> @this, ODataQueryOptions options = null)
        {
            Throw.IfNull(@this, "this");
            var oDataQuery = @this as IODataQueryable<TElement>;
            Throw<ArgumentException>.If(oDataQuery == null, () => "this: must implement " + typeof(IODataResult<TElement>));
            return oDataQuery.ExecuteQueryAsync(options);
        }

        /// <summary>
        /// Executes the given OData query
        /// </summary>
        public static IODataResult<TElement> ExecuteQuery<TElement>(this IQueryable<TElement> @this, ODataQueryOptions options = null)
        {
            return @this.ExecuteQueryAsync(options).GetResultWithOriginalException();
        }

        /// <summary>
        /// Provides aync OData query execution after applying the given execute expression. For example:
        /// <code>
        /// var count = await query.ExecuteAsync(q => q.Count());
        /// </code>
        /// </summary>
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
