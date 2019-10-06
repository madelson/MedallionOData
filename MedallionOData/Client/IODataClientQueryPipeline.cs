using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Medallion.OData.Client
{
    /// <summary>
    /// A pipeline for executing queries against a remote service
    /// </summary>
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
        /// <summary>
        /// Gets the content stream for the web response
        /// </summary>
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
        private readonly Func<Uri, Task<IODataWebResponse>> _getResponse;

        /// <summary>
        /// Constructs an instance of <see cref="DefaultODataClientQueryPipeline"/>
        /// </summary>
        public DefaultODataClientQueryPipeline()
        {
            this._getResponse = HttpClientWebResponse.CreateAsync;
        }

        internal DefaultODataClientQueryPipeline(Func<Uri, Task<Stream>> performWebRequest)
        {
            this._getResponse = url => Task.FromResult<IODataWebResponse>(new FuncWebResponse(performWebRequest, url));
        }

        IODataTranslationResult IODataClientQueryPipeline.Translate(Expression expression, ODataQueryOptions options)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(options, "options");

            var translator = new LinqToODataTranslator();
            var oDataQuery = translator.Translate(expression, out var rootQuery, out var postProcessor);
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

        Task<IODataWebResponse> IODataClientQueryPipeline.ReadAsync(Uri url)
        {
            Throw.IfNull(url, nameof(url));

            return this._getResponse(url);
        }

        private sealed class HttpClientWebResponse : IODataWebResponse
        {
            private static readonly HttpClient Client = new HttpClient();

            private readonly HttpResponseMessage _response;

            private HttpClientWebResponse(HttpResponseMessage response)
            {
                this._response = response;
            }

            public static async Task<IODataWebResponse> CreateAsync(Uri uri)
            {
                var response = await Client.GetAsync(uri).ConfigureAwait(false);
                return new HttpClientWebResponse(response);
            }

            void IDisposable.Dispose()
            {
                this._response.Dispose();
            }

            Task<Stream> IODataWebResponse.GetResponseStreamAsync()
            {
                return this._response.Content.ReadAsStreamAsync();
            }
        }

        private sealed class FuncWebResponse : IODataWebResponse
        {
            private readonly Func<Uri, Task<Stream>> _requestFunc;
            private readonly Uri _url;

            public FuncWebResponse(Func<Uri, Task<Stream>> requestFunc, Uri url)
            {
                this._requestFunc = requestFunc;
                this._url = url;
            }

            void IDisposable.Dispose() { }

            async Task<Stream> IODataWebResponse.GetResponseStreamAsync()
            {
                var task = this._requestFunc(this._url);
                if (task == null) { throw new InvalidOperationException("web request function must not return a null " + nameof(Task)); }
                var stream = await task.ConfigureAwait(false);
                if (stream == null) { throw new InvalidOperationException("web request function must not return a null " + nameof(Stream)); }
                return stream;
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
                
                int? inlineCount;
                if (result.TryGetValue("odata.count", out var inlineCountToken))
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
