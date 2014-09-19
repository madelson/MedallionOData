using Medallion.OData.Client;
using Medallion.OData.Trees;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    internal abstract class ODataSqlQuery : IQueryProvider, IOrderedQueryable
    {
        private readonly Expression expression;
        private readonly SqlSyntax syntax;
        private readonly SqlExecutor executor;
        private readonly string tableSql;

        protected ODataSqlQuery(Expression expression, SqlSyntax syntax, SqlExecutor executor)
            : this(syntax, executor)
        {
            Throw.IfNull(expression, "expression");
            
            this.expression = expression;
            this.tableSql = null;
        }

        protected ODataSqlQuery(string tableSql, SqlSyntax syntax, SqlExecutor executor)
            : this(syntax, executor)
        {
            Throw.If(string.IsNullOrEmpty(tableSql), "tableSql is required");

            this.expression = Expression.Constant(this);
            this.tableSql = tableSql;
        }

        private ODataSqlQuery(SqlSyntax syntax, SqlExecutor executor)
        {
            Throw.IfNull(syntax, "syntax");
            Throw.IfNull(executor, "executor");

            this.syntax = syntax;
            this.executor = executor;
        }

        #region ---- IQueryProvider implementation ----
        IQueryable<TQueryElement> IQueryProvider.CreateQuery<TQueryElement>(System.Linq.Expressions.Expression expression)
        {
            Throw.IfNull(expression, "expression");

            return new ODataSqlQuery<TQueryElement>(expression, this.syntax, this.executor);
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
        [DebuggerStepThrough]
        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumeratorInternal();
        }

        protected abstract IEnumerator GetEnumeratorInternal();

        Type IQueryable.ElementType
        {
            [DebuggerStepThrough] get { return this.GetElementTypeInternal(); }
        }

        protected abstract Type GetElementTypeInternal();

        System.Linq.Expressions.Expression IQueryable.Expression
        {
            [DebuggerStepThrough] get { return this.expression; }
        }

        IQueryProvider IQueryable.Provider
        {
            [DebuggerStepThrough] get { return this; }
        }
        #endregion

        #region ---- Execution ----
        private static readonly MethodInfo CastMethod = Helpers.GetMethod((IEnumerable e) => e.Cast<object>())
            .GetGenericMethodDefinition();

        protected object ExecuteCommon(Expression expression)
        {
            // translate
            List<Parameter> parameters;
            Type rootElementType;
            LinqToODataTranslator.ResultTranslator resultTranslator;
            var sql = this.ToSql(out parameters, out rootElementType, out resultTranslator);

            // execute
            var rawResults = this.executor.Execute(sql, parameters, rootElementType);
            var castRawResults = CastMethod.MakeGenericMethod(rootElementType)
                .InvokeWithOriginalException(null, new object[] { rawResults });
            // TODO does passing null here stop count from working?
            var results = resultTranslator((IEnumerable)castRawResults, inlineCount: null);

            return results;
        }

        private string ToSql(out List<Parameter> parameters, out Type rootElementType, out LinqToODataTranslator.ResultTranslator resultTranslator)
        {
            var translator = new LinqToODataTranslator();

            // TODO try-catch and change exception type
            IQueryable rootQuery;
            var oDataExpression = translator.Translate(expression, out rootQuery, out resultTranslator);
            rootElementType = rootQuery.ElementType;

            var queryExpression = oDataExpression as ODataQueryExpression;
            Throw<InvalidOperationException>.If(oDataExpression == null, "A queryable expression must translate to a query ODataExpression");
            Throw<InvalidOperationException>.If(queryExpression.InlineCount != ODataInlineCountOption.None, "Unexpected inline count option");

            // get the table SQL from the root query
            var tableQuery = rootQuery as ODataSqlQuery;
            Throw<InvalidOperationException>.If(
                tableQuery == null,
                () => "Translate: expected a root query query of type " + typeof(ODataSqlQuery) + "; found " + tableQuery
            );
            Throw<InvalidOperationException>.If(tableQuery.tableSql == null, "Invalid root query");

            // translate ODataExpression to SQL
            var sql = ODataToSqlTranslator.Translate(this.syntax, tableQuery.tableSql, queryExpression, out parameters);
            return sql;
        }
        #endregion

        public override string ToString()
        {
            var builder = new StringBuilder();
            try
            {
                List<Parameter> parameters;
                Type rootElementType;
                LinqToODataTranslator.ResultTranslator resultTranslator;
                var sql = this.ToSql(out parameters, out rootElementType, out resultTranslator);
                builder.AppendLine("/*")
                    .AppendFormat(" * Materialize as {0}", rootElementType).AppendLine();
                parameters.ForEach(p => builder.AppendFormat(" * {0} = {1}", p.Name, p.Value).AppendLine());
                builder.AppendLine(" */")
                    .AppendLine()
                    .AppendLine(sql);
            }
            catch (Exception ex)
            {
                builder.AppendLine("Query could not be translated to SQL: ")
                    .AppendLine()
                    .Append(ex).AppendLine();
            }
            return builder.ToString();
        }
    }

    internal sealed class ODataSqlQuery<TElement> : ODataSqlQuery, IOrderedQueryable<TElement>
    {
        public ODataSqlQuery(Expression expression, SqlSyntax syntax, SqlExecutor executor)
            : base(expression, syntax, executor)
        {
        }

        public ODataSqlQuery(string tableSql, SqlSyntax syntax, SqlExecutor executor)
            : base(tableSql, syntax, executor)
        {
        }

        IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
        {
            var results = (IEnumerable<TElement>)this.ExecuteCommon(this.As<IQueryable>().Expression);
            return results.GetEnumerator();
        }

        [DebuggerStepThrough]
        protected override IEnumerator GetEnumeratorInternal()
        {
            return this.AsEnumerable().GetEnumerator();
        }

        [DebuggerStepThrough]
        protected override Type GetElementTypeInternal()
        {
            return typeof(TElement);
        }
    }
}
