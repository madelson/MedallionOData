using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Medallion.OData.Tests.Client
{
    public class ODataQueryContextTest
    {
        [Test]
        public void TestPassedPipelineIsUsed()
        {
            var mock = new Mock<IODataClientQueryPipeline>(MockBehavior.Strict);
            var context = new ODataQueryContext(mock.Object);
            var agg = UnitTestHelpers.AssertThrows<AggregateException>(() => context.Query(new Uri("http://localhost:80")).ToArray());            
            var innerMost = Traverse.Along(agg.As<Exception>(), e => e.InnerException).Last();
            Assert.That(innerMost, Is.InstanceOf<MockException>());
        }

        [Test]
        public void TestCreateRequestUri()
        {
            var builder = new UriBuilder("http://localhost:1/foo");

            var uri1 = ODataQueryContext.CreateRequestUri(builder.Uri, new NameValueCollection());
            uri1.ToString().ShouldEqual("http://localhost:1/foo?");

            builder.Query = "a=2";
            var uri2 = ODataQueryContext.CreateRequestUri(builder.Uri, new NameValueCollection());
            uri2.ToString().ShouldEqual("http://localhost:1/foo?a=2");

            var oDataParams = new NameValueCollection { ["$format"] = "json" };
            var uri3 = ODataQueryContext.CreateRequestUri(builder.Uri, oDataParams);
            uri3.ToString().ShouldEqual("http://localhost:1/foo?a=2&%24format=json");

            var relativeUriWithNoQuery = new Uri("/categories", UriKind.Relative);
            var uri4 = ODataQueryContext.CreateRequestUri(relativeUriWithNoQuery, new NameValueCollection());
            uri4.ToString().ShouldEqual("/categories?");

            var relativeUriWithEmptyQuery = new Uri("/categories?", UriKind.Relative);
            var uri5 = ODataQueryContext.CreateRequestUri(relativeUriWithEmptyQuery, new NameValueCollection());
            uri5.ToString().ShouldEqual("/categories?");

            var uri6 = ODataQueryContext.CreateRequestUri(relativeUriWithEmptyQuery, oDataParams);
            uri6.ToString().ShouldEqual("/categories?%24format=json");

            var relativeUriWithQuery = new Uri("/categories?&x=1", UriKind.Relative);
            var uri7 = ODataQueryContext.CreateRequestUri(relativeUriWithQuery, new NameValueCollection());
            uri7.ToString().ShouldEqual("/categories?x=1");

            var uri8 = ODataQueryContext.CreateRequestUri(relativeUriWithQuery, oDataParams);
            uri8.ToString().ShouldEqual("/categories?x=1&%24format=json");
        }

        [Test]
        public void TestPassedInWebRequestFunctionIsUsed()
        {
            var context = new ODataQueryContext(PerformWebRequest);

            var query = context.Query<Pig>("/pigs").Where(p => false).ToArray();

            query[0].Weight.ShouldEqual(120);
            query[0].Name.ShouldEqual("Babe");
            query[1].Weight.ShouldEqual(1);
            query[1].Name.ShouldEqual("Piglet");
            query.Length.ShouldEqual(2);

            Task<Stream> PerformWebRequest(Uri url)
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(new
                {
                    value = new[] { new Pig { Weight = 120, Name = "Babe" }, new Pig { Weight = 1, Name = "Piglet" } },
                }));
                writer.Flush();
                stream.Position = 0;
                return Task.FromResult<Stream>(stream);
            }
        }

        [Test]
        public void TestPassedInWebRequestFunctionIsValidated()
        {
            Assert.Throws<ArgumentNullException>(() => new ODataQueryContext(default(Func<Uri, Task<Stream>>)));

            var nullTaskContext = new ODataQueryContext(ReturnsNullTask);
            var ex = Assert.Throws<InvalidOperationException>(() => nullTaskContext.Query<Pig>("/pigs").ToArray());
            Assert.That(ex.ToString(), Does.Contain("must not return a null Task"));

            var nullStreamContext = new ODataQueryContext(ReturnsNullStream);
            ex = Assert.Throws<InvalidOperationException>(() => nullStreamContext.Query<Pig>("/pigs").ToArray());
            Assert.That(ex.ToString(), Does.Contain("must not return a null Stream"));

            Task<Stream> ReturnsNullTask(Uri url) => null;
            Task<Stream> ReturnsNullStream(Uri url) => Task.FromResult<Stream>(null);
        }

        private class Pig
        {
            public double Weight { get; set; }
            public string Name { get; set; }
        }
    }
}
