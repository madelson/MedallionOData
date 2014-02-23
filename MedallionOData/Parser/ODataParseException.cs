using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Parser
{
	internal class ODataParseException : Exception
	{
		public ODataParseException(string message)
			: base(message)
		{
		}
	}
}
