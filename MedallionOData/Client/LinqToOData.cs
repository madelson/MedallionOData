using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using Medallion.OData.Trees;

namespace Medallion.OData.Client
{
    /// <summary>
    ///     Provides a method to translate a Linq expression into a ODataQueryExpression.
    ///     From the ODataQueryExpression the OData query can be extracted.
    /// </summary>
    public class LinqToOData
    {
        /// <summary>
        ///     Translate the Expression to a NameValueCollection with each OData query part as key
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="options">The options.</param>
        /// <returns>NameValueCollection.</returns>
        public static NameValueCollection ToNameValueCollection(Expression expression, ODataQueryOptions options)
        {
            return Translate(expression, options).ToNameValueCollection();
        }

        /// <summary>
        ///     Translate the Linq Expression to an OData string
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        public static string ToODataExpressionLanguage(Expression expression, ODataQueryOptions options)
        {
            return Translate(expression, options).ToODataExpressionLanguage();
        }

        /// <summary>
        ///     Translates the specified expression into an ODataQueryExpression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="options">The options.</param>
        /// <returns>ODataQueryExpression.</returns>
        public static ODataQueryExpression Translate(Expression expression, ODataQueryOptions options)
        {
            var translator = new LinqToODataTranslator();
            IQueryable rootQuery;
            LinqToODataTranslator.ResultTranslator postProcessor;
            var oDataQuery = translator.Translate(expression, out rootQuery, out postProcessor);
            Throw.If(oDataQuery.Kind != ODataExpressionKind.Query, "expression: did not translate to a query expression");
            var oDataQueryWithOptions = ((ODataQueryExpression)oDataQuery).Update(format: options.Format, inlineCount: options.InlineCount ?? ((ODataQueryExpression)oDataQuery).InlineCount);
            return oDataQueryWithOptions;
        }
    }
}
