using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Moq;
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
    }
}
