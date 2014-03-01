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
        ITranslationResult Translate(Expression expression, ODataQueryOptions options);

        /// <summary>
        /// Step 2: makes the request to the remote service
        /// </summary>
        Task<IWebResponse> ReadAsync(Uri url);

        /// <summary>
        /// Step 3: serialize results
        /// </summary>
        Task<IDeserializationResult> DeserializeAsync(ITranslationResult translation, Stream response);
    }

    /// <summary>
    /// Represents the result of translating a linq expression to OData
    /// </summary>
    public interface ITranslationResult 
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
        Func<object, object> PostProcessor { get; }
    }

    /// <summary>
    /// Provides a generic interface which could be used to wrap the various implementations of web responses in .NET
    /// </summary>
    public interface IWebResponse : IDisposable
    {
        Task<Stream> GetResponseStreamAsync();
    }

    /// <summary>
    /// Represents the result of deserializing the response
    /// </summary>
    public interface IDeserializationResult
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
    public sealed class DefaultODataQueryPipeline : IODataClientQueryPipeline
    {
        ITranslationResult IODataClientQueryPipeline.Translate(Expression expression, ODataQueryOptions options)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(options, "options");

            var translator = new LinqToODataTranslator();
            IQueryable rootQuery;
            Func<object, object> postProcessor;
            var oDataQuery = translator.Translate(expression, out rootQuery, out postProcessor);
            Throw.If(oDataQuery.Kind != ODataExpressionKind.Query, "expression: did not translate to a query expression");

            var oDataQueryWithOptions = ((ODataQueryExpression)oDataQuery).Update(format: options.Format, inlineCount: options.InlineCount);

            return new TranslationResult(rootQuery, oDataQueryWithOptions, postProcessor);
        }

        private sealed class TranslationResult : Tuple<IQueryable, ODataQueryExpression, Func<object, object>>, ITranslationResult
        {
            public TranslationResult(IQueryable rootQuery, ODataQueryExpression oDataQuery, Func<object, object> postProcessor)
                : base(rootQuery, oDataQuery, postProcessor)
            {
            }

            IQueryable ITranslationResult.RootQuery { get { return this.Item1; } }
            ODataQueryExpression ITranslationResult.ODataQuery { get { return this.Item2; } }
            Func<object, object> ITranslationResult.PostProcessor { get { return this.Item3; } }
        }

        async Task<IWebResponse> IODataClientQueryPipeline.ReadAsync(Uri url)
        {
            Throw.IfNull(url, "url");

            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return new HttpWebResponseWebResponse(response);
        }

        private sealed class HttpWebResponseWebResponse : IWebResponse
        {
            private HttpWebResponse _response;

            public HttpWebResponseWebResponse(HttpWebResponse response)
            {
                this._response = response;
            }

            Task<Stream> IWebResponse.GetResponseStreamAsync()
            {
                return Task.FromResult(this._response.GetResponseStream());
            }

            void IDisposable.Dispose()
            {
                this._response.Dispose();
            }
        }

        async Task<IDeserializationResult> IODataClientQueryPipeline.DeserializeAsync(ITranslationResult translation, Stream response)
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

                var values = (IReadOnlyList<object>)result["values"].ToObject(translation.RootQuery.ElementType.MakeArrayType());
                return new JsonDeserializationResult(inlineCount, values);
            }
        }

        private class JsonDeserializationResult : Tuple<int?, IReadOnlyList<object>>, IDeserializationResult
        {
            public JsonDeserializationResult(int? inlineCount, IReadOnlyList<object> values)
                : base(inlineCount, values)
            {
            }

            int? IDeserializationResult.InlineCount { get { return this.Item1; } }
            IReadOnlyList<object> IDeserializationResult.Values { get { return this.Item2; } }
        }
    }
}
