using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Parser
{
    /// <summary>
    /// Represents an error during the parsing of an OData url
    /// </summary>
	[Serializable]
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

        internal ODataParseException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
	}
}
