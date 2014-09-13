using Medallion.OData.Client;
using Medallion.OData.Trees;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Sql
{
    internal abstract class SqlQueryProviderForOData : IQueryProvider, IOrderedQueryable
    {
        private readonly Expression expression;
        private readonly DatabaseProvider databaseProvider;
        private readonly string tableSql;

        protected SqlQueryProviderForOData(Expression expression, DatabaseProvider databaseProvider)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(databaseProvider, "databaseProvider");

            this.expression = expression;
            this.databaseProvider = databaseProvider;
            this.tableSql = null;
        }

        protected SqlQueryProviderForOData(string tableSql, DatabaseProvider databaseProvider)
        {
            Throw.If(string.IsNullOrEmpty(tableSql), "tableSql is required");
            Throw.IfNull(databaseProvider, "databaseProvider");

            this.expression = Expression.Constant(this);
            this.databaseProvider = databaseProvider;
            this.tableSql = tableSql;
        }

        #region ---- IQueryProvider implementation ----
        IQueryable<TQueryElement> IQueryProvider.CreateQuery<TQueryElement>(System.Linq.Expressions.Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return new SqlQueryProviderForOData<TQueryElement>(expression, this.databaseProvider);
        }

        IQueryable IQueryProvider.CreateQuery(System.Linq.Expressions.Expression expression)
        {
            Throw.IfNull(expression, "expression");
            var queryElementType = expression.Type.GetGenericArguments(typeof(IQueryable<>)).SingleOrDefault();
            Throw.If(queryElementType == null, "expression: must be of type IQueryable<T>");

            return (IQueryable)Helpers.GetMethod((IQueryProvider p) => p.CreateQuery<object>(null))
                .GetGenericMethodDefinition()
                .MakeGenericMethod(queryElementType)
                .Invoke(this, new[] { expression });
        }

        TResult IQueryProvider.Execute<TResult>(System.Linq.Expressions.Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return (TResult)this.ExecuteCommon(expression);
        }

        object IQueryProvider.Execute(System.Linq.Expressions.Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return Helpers.GetMethod((IQueryProvider p) => p.CreateQuery<object>(null))
                .GetGenericMethodDefinition()
                .MakeGenericMethod(expression.Type)
                .Invoke(this, new[] { expression });
        }
        #endregion

        #region ---- IQueryable implementation ----
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumeratorInternal();
        }

        protected abstract IEnumerator GetEnumeratorInternal();

        Type IQueryable.ElementType
        {
            get { return this.GetElementTypeInternal(); }
        }

        protected abstract Type GetElementTypeInternal();

        System.Linq.Expressions.Expression IQueryable.Expression
        {
            get { return this.expression; }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return this; }
        }
        #endregion

        #region ---- Execution ----
        protected object ExecuteCommon(Expression expression)
        {
            var translator = new LinqToODataTranslator();

            // TODO try-catch and change exception type
            IQueryable rootQuery;
            LinqToODataTranslator.ResultTranslator resultTranslator;
            var oDataExpression = translator.Translate(expression, out rootQuery, out resultTranslator);

            var queryExpression = oDataExpression as ODataQueryExpression;
            Throw<InvalidOperationException>.If(oDataExpression == null, "A queryable expression must translate to a query ODataExpression");
            Throw<InvalidOperationException>.If(queryExpression.InlineCount != ODataInlineCountOption.None, "Unexpected inline count option");

            // get the table SQL from the root query
            var tableQuery = rootQuery as SqlQueryProviderForOData;
            Throw<InvalidOperationException>.If(
                tableQuery == null,
                () => "Translate: expected a root query query of type " + typeof(SqlQueryProviderForOData) + "; found " + tableQuery
            );
            Throw<InvalidOperationException>.If(tableQuery.tableSql == null, "Invalid root query");

            // translate ODataExpression to SQL
            List<Parameter> parameters;
            var sql = ODataToSqlTranslator.Translate(this.databaseProvider, tableQuery.tableSql, (ODataQueryExpression)oDataExpression, out parameters);

            // execute
            // TODO does passing null here stop count from working?
            var results = resultTranslator(this.databaseProvider.Execute(sql, parameters, rootQuery.ElementType), inlineCount: null);

            return results;
        }
        #endregion
    }

    internal sealed class SqlQueryProviderForOData<TElement> : SqlQueryProviderForOData, IOrderedQueryable<TElement>
    {
        public SqlQueryProviderForOData(Expression expression, DatabaseProvider databaseProvider)
            : base(expression, databaseProvider)
        {
        }

        public SqlQueryProviderForOData(string tableSql, DatabaseProvider databaseProvider)
            : base(tableSql, databaseProvider)
        {
        }

        IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
        {
            var results = (IEnumerable<TElement>)this.ExecuteCommon(this.As<IQueryable>().Expression);
            return results.GetEnumerator();
        }

        protected override IEnumerator GetEnumeratorInternal()
        {
            return this.AsEnumerable().GetEnumerator();
        }

        protected override Type GetElementTypeInternal()
        {
            return typeof(TElement);
        }
    }
}
