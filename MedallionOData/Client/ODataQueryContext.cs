﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Client
{
    /// <summary>
    /// Provides an access point for creating OData queries
    /// </summary>
    public sealed class ODataQueryContext : IQueryProvider
    {
        private readonly IODataClientQueryPipeline _pipeline;

        /// <summary>
        /// Creates a query context with the given pipeline
        /// </summary>
        public ODataQueryContext(IODataClientQueryPipeline pipeline = null)
        {
            this._pipeline = pipeline ?? new DefaultODataClientQueryPipeline();
        }

        /// <summary>
        /// Creates a query context which uses the provided <paramref name="performWebRequest"/> function
        /// to perform the underlying web requests.
        /// 
        /// This is useful for injecting custom authentication or error handling steps into the pipeline without
        /// constructing an entire custom <see cref="IODataClientQueryPipeline"/>
        /// </summary>
        public ODataQueryContext(Func<Uri, Task<Stream>> performWebRequest)
            : this(new DefaultODataClientQueryPipeline(performWebRequest ?? throw new ArgumentNullException(nameof(performWebRequest))))
        {
        }

        #region ---- Query factory methods ----
        private static readonly MethodInfo QueryMethod = Helpers.GetMethod((ODataQueryContext p) => p.Query<object>(default(Uri)))
            .GetGenericMethodDefinition();

        /// <summary>
        /// Creates a query against the given uri
        /// </summary>
        public IQueryable<TElement> Query<TElement>(Uri url)
        {
            Throw.IfNull(url, "url");

            return new ODataQuery<TElement>(this, url);
        }

        /// <summary>
        /// Creates a query against the given uri
        /// </summary>
        public IQueryable<TElement> Query<TElement>(string url)
        {
            Throw.IfNull(url, "url");
            return this.Query<TElement>(new Uri(url, UriKind.RelativeOrAbsolute));
        }

        /// <summary>
        /// Creates a "dynamic" query against the given uri
        /// </summary>
        public IQueryable<ODataEntity> Query(Uri url)
        {
            return this.Query<ODataEntity>(url);
        }

        /// <summary>
        /// Creates a "dynamic" query against the given url
        /// </summary>
        public IQueryable<ODataEntity> Query(string url)
        {
            return this.Query<ODataEntity>(url);
        }
        #endregion

        #region ---- IQueryProvider implementation ----
        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
        {
            Throw.IfNull(expression, "expression");
            return new ODataQuery<TElement>(this, expression);
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            Throw.IfNull(expression, "expression");
            var queryElementType = expression.Type.GetGenericArguments(typeof(IQueryable<>)).SingleOrDefault();
            Throw.If(queryElementType == null, "expression: must be of type IQueryable<T>");

            return (IQueryable)Helpers.GetMethod((IQueryProvider p) => p.CreateQuery<object>(null))
                .GetGenericMethodDefinition()
                .MakeGenericMethod(queryElementType)
                .Invoke(this, new[] { expression });
        }

        TResult IQueryProvider.Execute<TResult>(Expression expression)
        {
            Throw.IfNull(expression, "expression");
            var result = this.ExecutePipelineAsync(expression).GetResultWithOriginalException();
            return (TResult)result.Value;
        }

        object IQueryProvider.Execute(Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return Helpers.GetMethod((IQueryProvider p) => p.Execute<object>(null))
                .GetGenericMethodDefinition()
                .MakeGenericMethod(expression.Type)
                .Invoke(this, new[] { expression });
        }
        #endregion

        #region ---- Execution logic ----
        private async Task<ExecuteResult> ExecutePipelineAsync(Expression expression, ODataQueryOptions options = null)
        {
            var translationResult = this._pipeline.Translate(expression, options ?? ODataQueryOptions.Default);

            var rootQuery = translationResult.RootQuery as ODataQuery;
            Throw<InvalidOperationException>.If(
                rootQuery == null,
                () => "Translate: expected a root query query of type " + typeof(ODataQuery) + "; found " + translationResult.RootQuery
            );
            Throw<InvalidOperationException>.If(rootQuery.Url == null, "Invalid root query");

            // build the request url, incorporating the OData parameters
            var requestUri = CreateRequestUri(rootQuery.Url, translationResult.ODataQuery.ToNameValueCollection());

            using (var response = await this._pipeline.ReadAsync(requestUri).ConfigureAwait(false))
            using (var responseStream = await response.GetResponseStreamAsync().ConfigureAwait(false))
            {
                var deserialized = await this._pipeline.DeserializeAsync(translationResult, responseStream).ConfigureAwait(false);
                var result = translationResult.PostProcessor(deserialized);
                return new ExecuteResult(result, deserialized.InlineCount);
            }
        }

        // internal for testing purposes
        internal static Uri CreateRequestUri(Uri baseUri, NameValueCollection oDataQueryParams)
        {
            // this is particularly annoying because the Uri class has such poor support for relative uris (e. g. you can't even get the query string)
            // similarly, UriBuilder does not support relative uris
            var baseUriString = baseUri.ToString();
            var baseQueryString = (baseUri.IsAbsoluteUri ? baseUri : new Uri(new Uri("http://localhost:80"), baseUriString)).Query;
            var baseQueryParams = HttpUtility.ParseQueryString(baseQueryString);
            foreach (string key in oDataQueryParams)
            {
                baseQueryParams[key] = oDataQueryParams[key];
            }
            
            // see http://stackoverflow.com/questions/3865975/namevaluecollection-to-url-query
            var finalQueryString = baseQueryParams.ToString();
            var finalUriString = baseUriString.Substring(startIndex: 0, length: baseUriString.Length - baseQueryString.Length) + "?" + finalQueryString;
            return new Uri(finalUriString, UriKind.RelativeOrAbsolute);
        }

        private sealed class ExecuteResult : Tuple<object, int?>
        {
            public ExecuteResult(object value, int? inlineCount) : base(value, inlineCount) { }

            public object Value { get { return this.Item1; } }
            public int? InlineCount { get { return this.Item2; } }
        }
        #endregion

        #region ---- IQueryable implementation ----
        private abstract class ODataQuery : IOrderedQueryable
        {
            private readonly Expression _expression;
            
            protected ODataQuery(ODataQueryContext provider, Expression expression)
            {
                this.Provider = provider;
                this._expression = expression;
            }

            protected ODataQuery(ODataQueryContext provider, Uri url)
            {
                this.Provider = provider;
                this.Url = url;
                this._expression = Expression.Constant(this);
            }

            protected ODataQueryContext Provider { get; }
            internal Uri Url { get; }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetNonGenericEnumerator();
            }

            protected abstract System.Collections.IEnumerator GetNonGenericEnumerator();

            Type IQueryable.ElementType
            {
                [DebuggerStepThrough]
                get { return this.ElementType; }
            }

            protected abstract Type ElementType { get; }

            Expression IQueryable.Expression
            {
                [DebuggerStepThrough]
                get { return this._expression; }
            }

            IQueryProvider IQueryable.Provider
            {
                [DebuggerStepThrough]
                get { return this.Provider; }
            }
        }

        private sealed class ODataQuery<TElement> : ODataQuery, IOrderedQueryable<TElement>, IODataQueryable<TElement>
        {
            public ODataQuery(ODataQueryContext provider, Expression expression)
                : base(provider, expression)
            {
            }

            public ODataQuery(ODataQueryContext provider, Uri url)
                : base(provider, url)
            {
            }

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
            {
                var result = (IEnumerable<TElement>)this.Provider.ExecutePipelineAsync(this.As<IQueryable>().Expression)
                    .GetResultWithOriginalException().Value;
                foreach (var element in result)
                {
                    yield return element; // using yield causes lazy evaluation of the query
                }
            }

            protected override System.Collections.IEnumerator GetNonGenericEnumerator()
            {
                return this.AsEnumerable().GetEnumerator();
            }

            protected override Type ElementType { get { return typeof(TElement); } }

            async Task<IODataResult<TElement>> IODataQueryable<TElement>.ExecuteQueryAsync(ODataQueryOptions options)
            {
                var result = await this.Provider.ExecutePipelineAsync(this.As<IQueryable>().Expression, options).ConfigureAwait(false);
                return new ODataResult<TElement>(((IEnumerable<TElement>)result.Value).ToArray(), result.InlineCount);
            }

            private sealed class ODataResult<T> : Tuple<IReadOnlyList<T>, int?>, IODataResult<T>
            {
                public ODataResult(IReadOnlyList<T> results, int? totalCount) : base(results, totalCount) { }

                IReadOnlyList<T> IODataResult<T>.Results { get { return this.Item1; } }
                int? IODataResult<T>.TotalCount { get { return this.Item2; } }
            }

            async Task<TResult> IODataQueryable<TElement>.ExecuteAsync<TResult>(Expression<Func<IQueryable<TElement>, TResult>> executeExpression)
            {
                Throw.IfNull(executeExpression, "executeExpression");

                var replacer = new ParameterReplacer(executeExpression.Parameters[0], this.As<IQueryable>().Expression);
                var replaced = replacer.Visit(executeExpression.Body);
                var result = await this.Provider.ExecutePipelineAsync(replaced).ConfigureAwait(false);
                return (TResult)result.Value;
            }
        }
        #endregion
    }
}
