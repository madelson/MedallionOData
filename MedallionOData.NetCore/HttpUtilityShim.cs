using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace System.Web
{
    internal static class HttpUtility
    {
        public static NameValueCollection ParseQueryString(string query) => Medallion.OData.QueryStringParser.ParseQueryString(query);
    }
}
