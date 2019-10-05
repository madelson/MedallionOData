using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;

namespace Medallion.OData.Service
{
    internal static class PaginationHelper
    {
        /// <summary>
        /// Optimizes pagination and counting by avoiding projection reads when <see cref="ODataQueryExpression.Top"/> is 0 and
        /// avoiding counts when on the last page of items (where count can be inferred)
        /// </summary>
        public static void Paginate<TElement>(IODataProjectResult<TElement> projectResult, out IEnumerable projectEnumerable, out int? count)
        {
            // if we're doing top(0), we can skip projection and just count. This is the case when a client calls .Count() on an OData queryable
            if (projectResult.ODataQuery.Top == 0)
            {
                projectEnumerable = Enumerable.Empty<object>();
                count = projectResult.ODataQuery.InlineCount == ODataInlineCountOption.AllPages
                    ? projectResult.InlineCountQuery.Count()
                    : default(int?);
                return;
            }

            // if we're counting...
            if (projectResult.ODataQuery.InlineCount == ODataInlineCountOption.AllPages)
            {
                // Enumerable.Cast to ensure in-memory cast
                var projectArray = Enumerable.Cast<object>(projectResult.ProjectedResultQuery).ToArray();
                projectEnumerable = projectArray;
                
                // ... and we're on the last page, we can skip the count

                if (!projectResult.ODataQuery.Top.HasValue
                    || projectArray.Length < projectResult.ODataQuery.Top.Value)
                {
                    count = projectResult.ODataQuery.Skip + projectArray.Length;
                }
                else
                {
                    count = projectResult.InlineCountQuery.Count();
                }
                return;
            }

            // otherwise, don't count
            projectEnumerable = projectResult.ProjectedResultQuery;
            count = null;
        }
    }
}
