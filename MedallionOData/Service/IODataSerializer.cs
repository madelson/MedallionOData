using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service
{
    public interface IODataSerializer
    {
        object Serialize<TElement>(IODataProjectResult<TElement> projectResult);
    }
}
