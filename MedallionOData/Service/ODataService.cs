using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service
{
    /// <summary>
    /// Wraps execution of an <see cref="IODataServiceQueryPipeline"/> in a reusable service object. This class is thread-safe so long
    /// as the given pipeline is also thread-safe
    /// </summary>
    public sealed class ODataService
    {
        private readonly IODataServiceQueryPipeline _pipeline;

        public ODataService(IODataServiceQueryPipeline pipeline = null)
        {
            Throw.IfNull(pipeline, "pipeline");
            this._pipeline = pipeline ?? new DefaultODataServiceQueryPipeline();
        }

        public Result Execute<TElement>(IQueryable<TElement> query, NameValueCollection urlQuery)
        {
            Throw.IfNull(query, "query");
            Throw.IfNull(urlQuery, "urlQuery");

            var parseResult = this._pipeline.Parse<TElement>(urlQuery);
            var filterResult = this._pipeline.Filter(query, parseResult);
            var projectResult = this._pipeline.Project(filterResult);
            var serialized = this._pipeline.Serialize(projectResult);

            return new Result(serialized, parseResult.ODataQuery.Format);
        }

        public sealed class Result
        {
            internal Result(object results, string format)
            {
                Throw.IfNull(results, "results");
                Throw.IfNull(format, "format");

                this.Results = results;
                this.Format = format;
            }

            public object Results { get; private set; }
            public string Format { get; private set; }
        }
    }
}
