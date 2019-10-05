using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Medallion.OData.Service;
using NUnit.Framework;

namespace Medallion.OData.Tests.Integration
{
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

        [OneTimeTearDown]
        public static void TearDown()
        {
            DisposeTestServer();
        } 
    }
}
