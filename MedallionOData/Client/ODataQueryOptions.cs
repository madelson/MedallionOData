using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    public sealed class ODataQueryOptions
    {
        private static ODataQueryOptions _defaultOptions = new ODataQueryOptions();
        public static ODataQueryOptions Default { get { return _defaultOptions; } }

        public ODataQueryOptions(string format = "json", ODataInlineCountOption? inlineCount = null) 
        {
            this.Format = format;
            this.InlineCount = inlineCount;
        }

        public ODataInlineCountOption? InlineCount { get; private set; }
        public string Format { get; private set; }
    }
}
