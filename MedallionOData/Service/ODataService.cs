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

        /// <summary>
        /// Constructs a service from the given pipeline
        /// </summary>
        public ODataService(IODataServiceQueryPipeline pipeline = null)
        {
            this._pipeline = pipeline ?? new DefaultODataServiceQueryPipeline();
        }

        /// <summary>
        /// Executes the given query with the given url query options. A <see cref="NameValueCollection"/>
        /// can be retrieved from a <see cref="Uri"/> using <see cref="System.Web.HttpUtility.ParseQueryString(string)"/>
        /// </summary>
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

        /// <summary>
        /// Executes the given query with the given url query options. Url query values can be retrieved in WebApi
        /// via the GetQueryNameValuePairs extension method on HttpRequestMessage
        /// </summary>
        public Result Execute<TElement>(IQueryable<TElement> query, IEnumerable<KeyValuePair<string, string>> urlQuery)
        {
            Throw.IfNull(urlQuery, "urlQuery");

            var nameValueCollection = new NameValueCollection();
            foreach (var kvp in urlQuery)
            {
                nameValueCollection.Add(kvp.Key, kvp.Value);
            }

            return this.Execute(query, nameValueCollection);
        }

        /// <summary>
        /// The result of executing a query locally in an OData service
        /// </summary>
        public sealed class Result
        {
            internal Result(object results, string format)
            {
                Throw.IfNull(results, "results");
                Throw.IfNull(format, "format");

                this.Results = results;
                this.Format = format;
            }

            /// <summary>
            /// The result data
            /// </summary>
            public object Results { get; private set; }
            /// <summary>
            /// The result format
            /// </summary>
            public string Format { get; private set; }
        }
    }
}
