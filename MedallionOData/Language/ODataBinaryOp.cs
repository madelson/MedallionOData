using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Language
{
    public enum ODataBinaryOp
    {
        // MA: organized into groups by increasing precedence level

        /// <summary>or</summary>
        Or = 100,

        /// <summary>and</summary>
		And = 200,

        /// <summary>eq</summary>
		Equal = 300,
        /// <summary>ne</summary>
		NotEqual = 301,
        /// <summary>gt</summary>
        GreaterThan = 302,
        /// <summary>ge</summary>
        GreaterThanOrEqual = 303,
        /// <summary>lt</summary>
        LessThan = 304,
        /// <summary>le</summary>
        LessThanOrEqual = 305,

        /// <summary>add</summary>
        Add = 306,
        /// <summary>sub</summary>
        Subtract = 307,
        /// <summary>mul</summary>
        Multiply = 308,
        /// <summary>div</summary>
        Divide = 309,
        /// <summary>mod</summary>
        Modulo = 310,
    }
}
