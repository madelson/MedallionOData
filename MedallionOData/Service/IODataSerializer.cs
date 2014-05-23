using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service
{
    /// <summary>
    /// An implementation of a serializer for OData services
    /// </summary>
    public interface IODataSerializer
    {
        /// <summary>
        /// Serializes the given result
        /// </summary>
        object Serialize<TElement>(IODataProjectResult<TElement> projectResult);
    }
}
