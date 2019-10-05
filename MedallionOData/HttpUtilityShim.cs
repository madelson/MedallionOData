#if NETSTANDARD1_5
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace System.Web
{
#pragma warning disable SA1649 // File name should match first type name
    internal static class HttpUtility
#pragma warning restore SA1649 // File name should match first type name
    {
        public static NameValueCollection ParseQueryString(string query) => Medallion.OData.QueryStringParser.ParseQueryString(query);
    }
}
#endif