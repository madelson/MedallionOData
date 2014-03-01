using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    public sealed class ODataQueryProvider : IQueryProvider
    {
        private readonly IODataClientQueryPipeline _pipeline;

        public ODataQueryProvider(IODataClientQueryPipeline pipeline = null)
        {
            this._pipeline = new DefaultODataQueryPipeline();
        }

        #region ---- Query factory methods ----
        private static readonly MethodInfo QueryMethod = Helpers.GetMethod((ODataQueryProvider p) => p.Query<object>(default(Uri)))
            .GetGenericMethodDefinition();

        public IQueryable<TElement> Query<TElement>(Uri url)
        {
            Throw.IfNull(url, "url");

            var result = this.As<IQueryProvider>()
                .CreateQuery<TElement>(Expression.Call(Expression.Constant(this), QueryMethod.MakeGenericMethod(typeof(TElement)), Expression.Constant(url)));
            return result;
        }

        public IQueryable<TElement> Query<TElement>(string url)
        {
            Throw.IfNull(url, "url");
            return this.Query<TElement>(new Uri(url));
        }
        #endregion

        #region ---- IQueryProvider implementation ----
        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
        {
            Throw.IfNull(expression, "expression");
            throw new NotImplementedException();
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
            var result = this.ExecutePipelineAsync(expression).Result;
            return (TResult)result.Value;
        }

        object IQueryProvider.Execute(Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return Helpers.GetMethod((IQueryProvider p) => p.CreateQuery<object>(null))
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

            using (var response = await this._pipeline.ReadAsync(rootQuery.Url).ConfigureAwait(false))
            {
                var responseStream = await response.GetResponseStreamAsync().ConfigureAwait(false);
                var deserialized = await this._pipeline.DeserializeAsync(translationResult, responseStream).ConfigureAwait(false);
                var result = translationResult.PostProcessor(deserialized);
                return new ExecuteResult(result, deserialized.InlineCount);
            }
        }

        private sealed class ExecuteResult : Tuple<object, int?>
        {
            public ExecuteResult(object value, int? inlineCount) : base(value, inlineCount) {  }

            public object Value { get { return this.Item1; } }
            public int? InlineCount { get { return this.Item2; } }
        }
        #endregion

        #region ---- IQueryable implementation ----
        private abstract class ODataQuery : IOrderedQueryable
        {
            protected readonly ODataQueryProvider _provider;
            private readonly Expression _expression;
            private readonly Uri _url;

            protected ODataQuery(ODataQueryProvider provider, Expression expression)
            {
                this._provider = provider;
                this._expression = expression;
            }

            protected ODataQuery(ODataQueryProvider provider, Uri url)
            {
                this._provider = provider;
                this._url = url;
                this._expression = Expression.Constant(this);
            }

            internal Uri Url { get { return this._url; } }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetNonGenericEnumerator();
            }

            protected abstract System.Collections.IEnumerator GetNonGenericEnumerator();

            Type IQueryable.ElementType
            {
                get { return this.ElementType; }
            }

            protected abstract Type ElementType { get; }

            Expression IQueryable.Expression
            {
                get { return this._expression; }
            }

            IQueryProvider IQueryable.Provider
            {
                get { return this._provider; }
            }
        }

        private sealed class ODataQuery<TElement> : ODataQuery, IOrderedQueryable<TElement>, IODataQueryable<TElement>
        {
            public ODataQuery(ODataQueryProvider provider, Expression expression)
                : base(provider, expression)
            {
            }

            public ODataQuery(ODataQueryProvider provider, Uri url)
                : base(provider, url)
            {
            }

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
            {
                var result = (IEnumerable<TElement>)this._provider.ExecutePipelineAsync(this.As<IQueryable>().Expression).Result.Value;
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

            async Task<IODataResult<TElement>> IODataQueryable<TElement>.ExecuteQueryAsync(ODataQueryOptions options = null)
            {
                var result = await this._provider.ExecutePipelineAsync(this.As<IQueryable>().Expression, options).ConfigureAwait(false);
                return new ODataResult<TElement>(((IEnumerable<TElement>)result.Value).ToArray(), result.InlineCount);
            }

            private sealed class ODataResult<TElement> : Tuple<IReadOnlyList<TElement>, int?>, IODataResult<TElement>
            {
                public ODataResult(IReadOnlyList<TElement> results, int? totalCount) : base(results, totalCount) { }

                IReadOnlyList<TElement> IODataResult<TElement>.Results { get { return this.Item1; } }
                int? IODataResult<TElement>.TotalCount { get { return this.Item2; } }
            }

            async Task<TResult> IODataQueryable<TElement>.ExecuteAsync<TResult>(Expression<Func<IQueryable<TElement>, TResult>> executeExpression)
            {
                Throw.IfNull(executeExpression, "executeExpression");

                var replacer = new ParameterReplacer(executeExpression.Parameters[0], this.As<IQueryable>().Expression);
                var replaced = replacer.Visit(executeExpression.Body);
                var result = await this._provider.ExecutePipelineAsync(replaced).ConfigureAwait(false);
                return (TResult)result.Value;
            }

            private sealed class ParameterReplacer : ExpressionVisitor
            {
                private readonly ParameterExpression _parameter;
                private readonly Expression _replacement;
                public ParameterReplacer(ParameterExpression parameter, Expression replacement)
                {
                    this._parameter = parameter;
                    this._replacement = replacement;
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    return node == this._parameter
                        ? this._replacement
                        : base.VisitParameter(node);
                }
            }
        }
        #endregion
    }
}
