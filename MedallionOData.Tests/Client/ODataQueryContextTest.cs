using Medallion.OData.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Client
{
    [TestClass]
    public class ODataQueryContextTest
    {
        [TestMethod]
        public void TestPassedPipelineIsUsed()
        {
            var mock = new Mock<IODataClientQueryPipeline>(MockBehavior.Strict);
            var context = new ODataQueryContext(mock.Object);
            var agg = UnitTestHelpers.AssertThrows<AggregateException>(() => context.Query(new Uri("http://localhost:80")).ToArray());            
            var innerMost = Traverse.Along(agg.As<Exception>(), e => e.InnerException).Last();
            Assert.IsInstanceOfType(innerMost, typeof(MockException));
        }
    }
}
