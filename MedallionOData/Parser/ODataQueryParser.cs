using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Medallion.OData.Trees;

namespace Medallion.OData.Parser
{
    internal class ODataQueryParser
    {
        public static ODataQueryExpression Parse(Type elementType, string queryString)
        {
            return Parse(elementType, HttpUtility.ParseQueryString(queryString));
        }

        public static ODataQueryExpression Parse(Type elementType, NameValueCollection parameters)
        {
            Throw.IfNull(elementType, "elementType");
            Throw.IfNull(parameters, "parameters");

            ODataExpression filter = null;
            var filterString = parameters["$filter"];
            if (!string.IsNullOrEmpty(filterString))
            {
                var parser = new ODataExpressionLanguageParser(elementType, filterString);
                filter = parser.Parse();
            }

            IReadOnlyList<ODataSortKeyExpression> orderBy = Empty<ODataSortKeyExpression>.Array;
            var orderByString = parameters["$orderby"];
            if (!string.IsNullOrEmpty(orderByString))
            {
                var parser = new ODataExpressionLanguageParser(elementType, orderByString);
                orderBy = parser.ParseSortKeyList();
            }

            int? top = null;
            var topString = parameters["$top"];
            if (!string.IsNullOrEmpty(topString))
            {
                int topValue;
                if (!int.TryParse(topString, out topValue))
                {
                    throw new ODataParseException("Expected an integer value for $top: " + topString);
                }
                top = topValue;
            }

            var skip = 0;
            var skipString = parameters["$skip"];
            if (!string.IsNullOrEmpty(skipString))
            {
                if (!int.TryParse(skipString, out skip))
                {
                    throw new ODataParseException("Expected an integer value for $skip: " + skipString);
                }
            }

            IReadOnlyList<ODataSelectColumnExpression> select = Empty<ODataSelectColumnExpression>.Array;
            var selectString = parameters["$select"];
            if (!string.IsNullOrEmpty(selectString))
            {
                var parser = new ODataExpressionLanguageParser(elementType, selectString);
                select = parser.ParseSelectColumnList();
            }

            var formatParameter = parameters["$format"];
            string format;
            if (!string.IsNullOrEmpty(formatParameter))
            {
                Throw<ODataParseException>.If(string.IsNullOrWhiteSpace(formatParameter), "$format may not be whitespace");
                format = formatParameter;
            }
            else
            {
                format = null;
            }

            var inlineCount = ODataInlineCountOption.None;
            var inlineCountString = parameters["$inlinecount"];
            if (!string.IsNullOrEmpty(inlineCountString))
            {
                if (!Enum.TryParse(inlineCountString, ignoreCase: true, result: out inlineCount))
                {
                    throw new ODataParseException("Unexpected value " + inlineCountString + " for $inlinecount");
                }
            }

            return ODataExpression.Query(
                filter: filter,
                orderBy: orderBy,
                top: top,
                skip: skip,
                select: select,
                format: format,
                inlineCount: inlineCount
            );
        }
    }
}
