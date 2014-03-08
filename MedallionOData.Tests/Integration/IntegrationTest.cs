using Medallion.OData.Client;
using Medallion.OData.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Medallion.OData.Trees;
using Newtonsoft.Json;

namespace Medallion.OData.Tests.Integration
{
    [TestClass]
    public class IntegrationTest
    {
        private static TestServer _testServer;

        [ClassInitialize]
        public static void SetUp(TestContext context)
        {
            var service = new ODataService();
            _testServer = new TestServer(url =>
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
            if (_testServer != null)
            {
                _testServer.As<IDisposable>().Dispose();
                _testServer = null;
            }
        }

        [TestMethod]
        public void IntegrationSimpleQuery()
        {
            this.Test<Customer, Customer>("customers", c => c, expected: CustomersContext.GetCustomers());
        }

        [TestMethod]
        public void IntegrationWhere()
        {
            this.Test<Customer, Customer>(
                "customers", 
                cc => cc.Where(c => c.CompanyId.HasValue && c.Name.StartsWith("Albert")), 
                expected: CustomersContext.GetCustomers()
                    .Where(c => c.CompanyId.HasValue && c.Name.StartsWith("Albert"))
            );
        }

        [TestMethod]
        public void IntegrationSort()
        {
            this.Test<Customer, Customer>(
                "customers",
                cc => cc.OrderBy(c => c.Name.Length).ThenByDescending(c => c.Name),
                expected: CustomersContext.GetCustomers()
                    .OrderBy(c => c.Name.Length).ThenByDescending(c => c.Name),
                orderMatters: true
            );
        }

        [TestMethod]
        public void IntegrationSelect()
        {
            this.Test(
                "customers",
                (IQueryable<Customer> cc) => cc.Select(c => new { c.Company, c.Name }),
                expected: CustomersContext.GetCustomers().Select(c => new { c.Company, c.Name })
            );
        }

        [TestMethod]
        public void IntegrationDynamicRowFilterAndProjectToSimpleType()
        {
            this.Test(
                "customers",
                (IQueryable<ODataRow> rows) => rows.Where(c => c.Get<ODataRow>("Company").Get<string>("Name") == "Mine")
                    .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.Company != null && c.Company.Name == "Mine")
                        .Select(c => c.Name)
            );
        }

        private static readonly ODataQueryProvider _provider = new ODataQueryProvider();
        private void Test<TSource, TResult>(string url, Func<IQueryable<TSource>, IQueryable<TResult>> query, IEnumerable<TResult> expected, IEqualityComparer<TResult> comparer = null, bool orderMatters = false)
        {
            var uri = new Uri(_testServer.Prefix + url);
            var rootQuery = _provider.Query<TSource>(uri);
            var resultQuery = query(rootQuery);
            var result = resultQuery.ToArray();
            result.CollectionShouldEqual(expected, orderMatters: orderMatters, comparer: comparer);
        }
    }
}
