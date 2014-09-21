using Medallion.OData.Client;
using Medallion.OData.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Tests.Integration
{
    [TestClass]
    public class InMemoryODataEntityIntegrationTest : IntegrationTestBase<InMemoryODataEntityIntegrationTest>
    {
        protected override TestServer CreateTestServer()
        {
            var service = new ODataService();
            var dynamicQuery = CustomersContext.GetCustomers()
                .Select(ToODataEntity)
                .Cast<ODataEntity>()
                .ToArray()
                .AsQueryable();

            return new TestServer(url =>
            {
                var result = service.Execute(dynamicQuery, HttpUtility.ParseQueryString(url.Query));
                return result.Results.ToString();
            });
        }

        private static object ToODataEntity(object obj)
        {
            if (obj is Customer || obj is Company)
            {
                var props = obj.GetType().GetProperties()
                    .Select(pi => KeyValuePair.Create(pi.Name, ToODataEntity(pi.GetValue(obj))));
                return new ODataEntity(props);
            }

            return obj;
        }

        protected override bool NullCoalescingSupported { get { return false; } }
        protected override bool FullNumericMixupSupported { get { return false; } }

        [ClassCleanup]
        public static void TearDown()
        {
            DisposeTestServer();
        } 
    }
}
