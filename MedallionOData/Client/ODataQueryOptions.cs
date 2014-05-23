using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    /// <summary>
    /// Options for query execution
    /// </summary>
    public sealed class ODataQueryOptions
    {
        private static ODataQueryOptions _defaultOptions = new ODataQueryOptions();
        internal static ODataQueryOptions Default { get { return _defaultOptions; } }

        /// <summary>
        /// Constructs a new instance of <see cref="ODataQueryOptions"/>
        /// </summary>
        public ODataQueryOptions(string format = "json", ODataInlineCountOption? inlineCount = null) 
        {
            this.Format = format;
            this.InlineCount = inlineCount;
        }

        /// <summary>
        /// Optionally specifies an inline count option for the query
        /// </summary>
        public ODataInlineCountOption? InlineCount { get; private set; }
        /// <summary>
        /// Determines the query format
        /// </summary>
        public string Format { get; private set; }
    }
}
