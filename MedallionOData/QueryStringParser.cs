using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData
{
    /// <summary>
    /// This class acts as a shim for <see cref="System.Web.HttpUtility.ParseQueryString(string)"/> for .NET standard.
    /// Due to an abundance of caution, we will use the native implementation in .NET framework. This class is not behind
    /// a preprocessor directive to allow for tests that do a direct comparison between the two
    /// </summary>
    internal static class QueryStringParser
    {
        private static readonly char[] ParameterSeparator = new[] { '&' },
            NameAndValueSeparator = new[] { '=' };

        public static NameValueCollection ParseQueryString(string value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            var valueToUse = value.StartsWith("?", StringComparison.Ordinal)
                ? value.Substring(1)
                : value;
            var result = new QueryStringParameterCollection();
            
            if (valueToUse.Length == 0) { return result; }

            var parameters = valueToUse.Split(ParameterSeparator);
            foreach (var parameter in parameters)
            {
                var nameAndValue = parameter.Split(NameAndValueSeparator, count: 2);
                switch (nameAndValue.Length)
                {
                    case 1:
                        result.Add(null, WebUtility.UrlDecode(nameAndValue[0]));
                        break;
                    case 2:
                        result.Add(WebUtility.UrlDecode(nameAndValue[0]), WebUtility.UrlDecode(nameAndValue[1]));
                        break;
                    default:
                        throw Throw.UnexpectedCase(nameAndValue.Length);
                }
            }

            return result;
        }

        private sealed class QueryStringParameterCollection : NameValueCollection
        {
            public QueryStringParameterCollection() : base(StringComparer.OrdinalIgnoreCase) { }

            public override string ToString()
            {
                var builder = new StringBuilder();
                var isFirstParameter = true;
                for (var i = 0; i < this.Count; ++i)
                {
                    var key = this.GetKey(i);
                    var values = this.GetValues(i);
                    foreach (var value in values)
                    {
                        if (!isFirstParameter) { builder.Append('&'); }
                        else { isFirstParameter = false; }

                        if (key != null) { builder.Append(WebUtility.UrlEncode(key)).Append('='); }
                        builder.Append(WebUtility.UrlEncode(value));
                    } 
                }

                return builder.ToString();
            }
        }
    }
}
