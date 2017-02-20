using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Parser
{
    /// <summary>
    /// Represents an error during the parsing of an OData url
    /// </summary>
#if !NETCORE
    [SerializableAttribute]
#endif
    public sealed class ODataParseException : Exception
	{
        /// <summary>
        /// Default constructor
        /// </summary>
        public ODataParseException() { }

        /// <summary>
        /// Message constructor
        /// </summary>
        public ODataParseException(string message)
            : base(message) { }

        /// <summary>
        /// Message and inner exception constructor
        /// </summary>
        public ODataParseException(string message, Exception inner)
            : base(message, inner) { }

#if !NETCORE
        internal ODataParseException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
    }
}
