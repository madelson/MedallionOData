using Medallion.OData.Parser;
using Medallion.OData.Trees;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service
{
    #region ---- Interfaces ----
    /// <summary>
    /// Provides a pipeline of steps to be performed by an OData service endpoint
    /// </summary>
    public interface IODataServiceQueryPipeline
    {
        /// <summary>
        /// Step 1
        /// Parses the given url to an <see cref="ODataExpression"/>
        /// </summary>
        IODataParseResult Parse<TElement>(NameValueCollection urlQuery);
        /// <summary>
        /// Step 2
        /// Applies filtering to the given query based on the parse results
        /// </summary>
        IODataFilterResult<TElement> Filter<TElement>(IQueryable<TElement> query, IODataParseResult parseResult);
        /// <summary>
        /// Step 3
        /// Applies projection to the filtered results
        /// </summary>
        IODataProjectResult<TElement> Project<TElement>(IODataFilterResult<TElement> filterResult);
        /// <summary>
        /// Step 4
        /// Serializes the results of the query
        /// </summary>
        object Serialize<TElement>(IODataProjectResult<TElement> projectResult);
    }

    public interface IODataParseResult
    {
        /// <summary>
        /// The <see cref="ODataExpression"/> represented by the query string
        /// </summary>
        ODataQueryExpression ODataQuery { get; }
    }

    public interface IODataFilterResult<TElement> : IODataParseResult
    {
        /// <summary>
        /// The query containing the result entities
        /// </summary>
        IQueryable<TElement> ResultQuery { get; }
        /// <summary>
        /// A query containing all entities to be included in an inline count call
        /// </summary>
        IQueryable<TElement> InlineCountQuery { get; }
    }

    public interface IODataProjectResult<TElement> : IODataFilterResult<TElement>
    {
        /// <summary>
        /// A version of the result query that will only "pull in" selected properties
        /// </summary>
        IQueryable ProjectedResultQuery { get; }
        /// <summary>
        /// Maps selected properties to their paths in the <see cref="ProjectedResultQuery"/>
        /// </summary>
        IReadOnlyDictionary<ODataSelectColumnExpression, IReadOnlyList<PropertyInfo>> ProjectMapping { get; }
    }
    #endregion

    #region ---- Implementation ----
    public sealed class DefaultODataServiceQueryPipeline : IODataServiceQueryPipeline
    {
        private readonly IReadOnlyDictionary<string, IODataSerializer> _serializersByFormat;

        public DefaultODataServiceQueryPipeline(IEnumerable<KeyValuePair<string, IODataSerializer>> serializersByFormat = null)
        {
            var dict = new Dictionary<string, IODataSerializer>(StringComparer.OrdinalIgnoreCase);
            if (serializersByFormat == null)
            {
                dict.Add("json", new ODataJsonSerializer());
            }
            else
            {
                foreach (var kvp in serializersByFormat)
                {
                    Throw<ArgumentException>.If(string.IsNullOrWhiteSpace(kvp.Key), () => string.Format("Invalid format '{0}'", kvp.Key));
                    Throw.IfNull(kvp.Value, "serializer");
                    dict[kvp.Key] = kvp.Value;
                }
            }
            this._serializersByFormat = dict;
        }

        IODataParseResult IODataServiceQueryPipeline.Parse<TElement>(NameValueCollection urlQuery)
        {
            Throw.IfNull(urlQuery, "urlQuery");

            var oDataQuery = ODataQueryParser.Parse(typeof(TElement), urlQuery);
            return new Result<TElement>(oDataQuery);
        }

        IODataFilterResult<TElement> IODataServiceQueryPipeline.Filter<TElement>(IQueryable<TElement> query, IODataParseResult parseResult)
        {
            Throw.IfNull(query, "query");
            Throw.IfNull(parseResult, "parseResult");

            IQueryable<TElement> inlineCountQuery;
            var resultQuery = ODataQueryFilter.Apply(query, parseResult.ODataQuery, out inlineCountQuery);
            return new Result<TElement>(parseResult.ODataQuery, resultQuery: resultQuery, inlineCountQuery: inlineCountQuery);
        }

        IODataProjectResult<TElement> IODataServiceQueryPipeline.Project<TElement>(IODataFilterResult<TElement> filterResult)
        {
            Throw.IfNull(filterResult, "filterResult");

            var projectResult = ODataQueryProjector.Project(filterResult.ResultQuery, filterResult.ODataQuery.Select);
            return new Result<TElement>(
                filterResult.ODataQuery,
                resultQuery: filterResult.ResultQuery,
                inlineCountQuery: filterResult.InlineCountQuery,
                projectMapping: projectResult.Mapping,
                projectedResultQuery: projectResult.Query
            );
        }

        object IODataServiceQueryPipeline.Serialize<TElement>(IODataProjectResult<TElement> projectResult)
        {
            Throw.IfNull(projectResult, "projectResult");
            IODataSerializer serializer;
            Throw<NotSupportedException>.If(!this._serializersByFormat.TryGetValue(projectResult.ODataQuery.Format, out serializer), "No serializer is available for the given format");

            var result = serializer.Serialize(projectResult);
            return result;
        }

        private class Result<TElement> : IODataProjectResult<TElement>
        {
            private readonly IQueryable _projectedResultQuery;
            private readonly IReadOnlyDictionary<ODataSelectColumnExpression, IReadOnlyList<PropertyInfo>> _projectMapping;
            private readonly IQueryable<TElement> _resultQuery, _inlineCountQuery;
            private readonly ODataQueryExpression _oDataQuery;

            public Result(ODataQueryExpression oDataQuery, 
                IQueryable<TElement> resultQuery = null, 
                IQueryable<TElement> inlineCountQuery = null,
                IReadOnlyDictionary<ODataSelectColumnExpression, IReadOnlyList<PropertyInfo>> projectMapping = null,
                IQueryable projectedResultQuery = null)
            {
                Throw.IfNull(oDataQuery, "oDataQuery");
                Throw<ArgumentNullException>.If((resultQuery == null) != (inlineCountQuery == null), "resultQuery/inlineCountQuery: must be null together");
                Throw<ArgumentNullException>.If((projectMapping == null) != (projectedResultQuery == null), "projectMapping/projectedResultQuery: must be null together");
                Throw<ArgumentNullException>.If(projectMapping != null && inlineCountQuery == null, "projectMapping: if specified, inlineCountQuery is required");

                this._oDataQuery = oDataQuery;
                this._resultQuery = resultQuery;
                this._inlineCountQuery = inlineCountQuery;
                this._projectMapping = projectMapping;
                this._projectedResultQuery = projectedResultQuery;
            }

            private static T GetChecked<T>(T value, string propertyName)
            {
                Throw<InvalidOperationException>.If(value == null, () => propertyName + " is not specified for this result");
                return value;
            }

            IQueryable IODataProjectResult<TElement>.ProjectedResultQuery
            {
                get { return GetChecked(this._projectedResultQuery, "ProjectedResultQuery"); }
            }

            IReadOnlyDictionary<ODataSelectColumnExpression, IReadOnlyList<PropertyInfo>> IODataProjectResult<TElement>.ProjectMapping
            {
                get { return GetChecked(this._projectMapping, "ProjectMapping"); }
            }

            IQueryable<TElement> IODataFilterResult<TElement>.ResultQuery
            {
                get { return GetChecked(this._resultQuery, "ResultQuery"); }
            }

            IQueryable<TElement> IODataFilterResult<TElement>.InlineCountQuery
            {
                get { return GetChecked(this._inlineCountQuery, "InlineCountQuery"); }
            }

            ODataQueryExpression IODataParseResult.ODataQuery
            {
                get { return GetChecked(this._oDataQuery, "ODataQuery"); }
            }
        }
    }
    #endregion
}
