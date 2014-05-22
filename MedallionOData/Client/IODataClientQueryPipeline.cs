using Medallion.OData.Trees;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Client
{
    public interface IODataClientQueryPipeline
    {
        /// <summary>
        /// Step 1: translates a Linq expression to the information needed to make a request to the remote service
        /// </summary>
        IODataTranslationResult Translate(Expression expression, ODataQueryOptions options);

        /// <summary>
        /// Step 2: makes the request to the remote service
        /// </summary>
        Task<IODataWebResponse> ReadAsync(Uri url);

        /// <summary>
        /// Step 3: serialize results
        /// </summary>
        Task<IODataDeserializationResult> DeserializeAsync(IODataTranslationResult translation, Stream response);
    }

    /// <summary>
    /// Represents the result of translating a linq expression to OData
    /// </summary>
    public interface IODataTranslationResult 
    {
        /// <summary>
        /// The root <see cref="IQueryable"/> of the original query expression
        /// </summary>
        IQueryable RootQuery { get; }
        /// <summary>
        /// The <see cref="ODataExpression"/> representing the OData component of the url
        /// </summary>
        ODataQueryExpression ODataQuery { get; }
        /// <summary>
        /// A post-processor function to be applied to the result after serialization
        /// </summary>
        Func<IODataDeserializationResult, object> PostProcessor { get; }
    }

    /// <summary>
    /// Provides a generic interface which could be used to wrap the various implementations of web responses in .NET
    /// </summary>
    public interface IODataWebResponse : IDisposable
    {
        Task<Stream> GetResponseStreamAsync();
    }

    /// <summary>
    /// Represents the result of deserializing the response
    /// </summary>
    public interface IODataDeserializationResult
    {
        /// <summary>
        /// Contains the result of the inline count option, if available
        /// </summary>
        int? InlineCount { get; }

        /// <summary>
        /// Contains the deserialized values
        /// </summary>
        IReadOnlyList<object> Values { get; }
    }

    /// <summary>
    /// Provides a default implementation of a client-side pipeline
    /// </summary>
    public sealed class DefaultODataClientQueryPipeline : IODataClientQueryPipeline
    {
        IODataTranslationResult IODataClientQueryPipeline.Translate(Expression expression, ODataQueryOptions options)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(options, "options");

            var translator = new LinqToODataTranslator();
            IQueryable rootQuery;
            LinqToODataTranslator.ResultTranslator postProcessor;
            var oDataQuery = translator.Translate(expression, out rootQuery, out postProcessor);
            Throw.If(oDataQuery.Kind != ODataExpressionKind.Query, "expression: did not translate to a query expression");

            var oDataQueryWithOptions = ((ODataQueryExpression)oDataQuery).Update(format: options.Format, inlineCount: options.InlineCount ?? ((ODataQueryExpression)oDataQuery).InlineCount);

            return new TranslationResult(rootQuery, oDataQueryWithOptions, r => postProcessor(r.Values, r.InlineCount));
        }

        private sealed class TranslationResult : Tuple<IQueryable, ODataQueryExpression, Func<IODataDeserializationResult, object>>, IODataTranslationResult
        {
            public TranslationResult(IQueryable rootQuery, ODataQueryExpression oDataQuery, Func<IODataDeserializationResult, object> postProcessor)
                : base(rootQuery, oDataQuery, postProcessor)
            {
            }

            IQueryable IODataTranslationResult.RootQuery { get { return this.Item1; } }
            ODataQueryExpression IODataTranslationResult.ODataQuery { get { return this.Item2; } }
            Func<IODataDeserializationResult, object> IODataTranslationResult.PostProcessor { get { return this.Item3; } }
        }

        async Task<IODataWebResponse> IODataClientQueryPipeline.ReadAsync(Uri url)
        {
            Throw.IfNull(url, "url");

            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return new HttpWebResponseWebResponse(response);
        }

        private sealed class HttpWebResponseWebResponse : IODataWebResponse
        {
            private HttpWebResponse _response;

            public HttpWebResponseWebResponse(HttpWebResponse response)
            {
                this._response = response;
            }

            Task<Stream> IODataWebResponse.GetResponseStreamAsync()
            {
                return Task.FromResult(this._response.GetResponseStream());
            }

            void IDisposable.Dispose()
            {
                this._response.Dispose();
            }
        }

        async Task<IODataDeserializationResult> IODataClientQueryPipeline.DeserializeAsync(IODataTranslationResult translation, Stream response)
        {
            Throw.IfNull(translation, "translation");
            Throw.IfNull(response, "response");
            Throw<NotSupportedException>.If(
                !StringComparer.OrdinalIgnoreCase.Equals(translation.ODataQuery.Format, "json"),
                () => "Unexpected format " + translation.ODataQuery.Format
            );

            using (var reader = new StreamReader(response))
            {
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<JObject>(json);

                JToken inlineCountToken;
                int? inlineCount;
                if (result.TryGetValue("odata.count", out inlineCountToken))
                {
                    inlineCount = inlineCountToken.Value<int?>();
                }
                else
                {
                    inlineCount = null;
                }

                var jValues = result["value"];
                Throw<InvalidOperationException>.If(!(jValues is JArray), () => "Expected 'value' json array property. Found '" + jValues + "'");
                var values = (IReadOnlyList<object>)jValues.ToObject(translation.RootQuery.ElementType.MakeArrayType());
                return new JsonDeserializationResult(inlineCount, values);
            }
        }

        private class JsonDeserializationResult : Tuple<int?, IReadOnlyList<object>>, IODataDeserializationResult
        {
            public JsonDeserializationResult(int? inlineCount, IReadOnlyList<object> values)
                : base(inlineCount, values)
            {
            }

            int? IODataDeserializationResult.InlineCount { get { return this.Item1; } }
            IReadOnlyList<object> IODataDeserializationResult.Values { get { return this.Item2; } }
        }
    }
}
