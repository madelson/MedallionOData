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
    public class EntityFrameworkIntegrationTest : IntegrationTestBase<EntityFrameworkIntegrationTest>
    {
        protected override TestServer CreateTestServer()
        {
            var service = new ODataService();
            return new TestServer(url =>
            {
                using (var db = new CustomersContext())
                {
                    var query = db.Customers;
                    var result = service.Execute(query, HttpUtility.ParseQueryString(url.Query));
                    return result.Results.ToString();
                }
            });
        }

        [ClassCleanup]
        public static void TearDown()
        {
            DisposeTestServer();
        } 
    }
}
