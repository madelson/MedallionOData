using Medallion.OData.Client;
using Medallion.OData.Service;
using Medallion.OData.Trees;
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
    public class IntegrationTestDynamic
    {
        private static TestServer _testServer;

        [ClassInitialize]
        public static void SetUp(TestContext context)
        {
            var service = new ODataService();
            var dynamicQuery = CustomersContext.GetCustomers()
                .Select(ToODataEntity)
                .Cast<ODataEntity>()
                .ToArray()
                .AsQueryable();

            _testServer = new TestServer(url =>
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
        public void IntegrationDynamicSimpleQuery()
        {
            this.Test<Customer, Customer>("customers", c => c, expected: CustomersContext.GetCustomers());
        }

        [TestMethod]
        public void IntegrationDynamicWhere()
        {
            this.Test<Customer, Customer>(
                "customers",
                cc => cc.Where(c => c.CompanyId.HasValue && c.Name.StartsWith("Albert")),
                expected: CustomersContext.GetCustomers()
                    .Where(c => c.CompanyId.HasValue && c.Name.StartsWith("Albert"))
            );
        }

        [TestMethod]
        public void IntegrationDynamicSort()
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
        public void IntegrationDynamicSelect()
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
                (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<ODataEntity>("Company") != null && c.Get<ODataEntity>("Company").Get<string>("Name") == "Mine")
                    .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.Company != null && c.Company.Name == "Mine")
                        .Select(c => c.Name)
            );
        }

        [TestMethod]
        public void IntegrationDynamicWithIntDoubleMixup()
        {
            this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<double>("AwardCount") == 5)
                    .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.AwardCount == 5)
                        .Select(c => c.Name)
            );

            this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) => rows.Select(c => new { @int = c.Get<double?>("AwardCount") }),
                expected: CustomersContext.GetCustomers().Select(c => new { @int = (double?)c.AwardCount })
            );

            // we can't do this test because an in-memory ODataEntity will have int as award count
            this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<double>("AwardCount") > 4.7)
                    .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.AwardCount > 4.7)
                        .Select(c => c.Name)
            );

            // we can't do this test because in-memory ODataEntity will have double as salary, but the other
            // side will guess int after seeing "Salary gt 50000"
            //this.Test(
            //    "customers",
            //    (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<double?>("Salary") > 50000)
            //        .Select(c => c.Get<string>("Name")),
            //        expected: CustomersContext.GetCustomers().Where(c => c.Salary > 50000)
            //            .Select(c => c.Name)
            //);
        }

        [TestMethod]
        public void IntegrationDynamicRowComplexQuery()
        {
            this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) => rows.Select(r => new { b = r.Get<ODataEntity>("Company"), c = r.Get<string>("Name").Length * 2 })
                    .Where(t => t.b == null || t.b.Get<string>("Name").Length % 3 != t.c % 3)
                    .Select(t => t.c),
                expected: CustomersContext.GetCustomers().Select(c => new { b = c.Company, c = c.Name.Length * 2 })
                    .Where(t => t.b == null || t.b.Name.Length % 3 != t.c % 3)
                    .Select(t => t.c)
            );
        }

        [TestMethod]
        public void IntegrationDynamicFilterByYear()
        {
            this.Test<Customer, Customer>(
                "customers",
                cc => cc.Where(c => c.Company != null && c.Company.DateClosed.HasValue && ((DateTime)c.Company.DateClosed).Year == 1988),
                expected: CustomersContext.GetCustomers().Where(c => c.Company != null && c.Company.DateClosed.HasValue && ((DateTime)c.Company.DateClosed).Year == 1988)
            );
        }

        [TestMethod]
        public void IntegrationTestDynamicFirstAndLast()
        {
            var first = this.CustomersODataQuery().OrderBy(c => c.Name).First();
            first.Name.ShouldEqual("A");
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Take(0).First());

            var firstOrDefault = this.CustomersODataQuery().OrderBy(c => c.Name).FirstOrDefault();
            firstOrDefault.Name.ShouldEqual("A");
            var firstOrDefault2 = this.CustomersODataQuery().Take(0).FirstOrDefault();
            firstOrDefault2.ShouldEqual(null);

            var firstPred = this.CustomersODataQuery().Where(c => c.Name.Contains("ert")).First(c => c.Name.Contains("A"));
            firstPred.Name.ShouldEqual("Albert");
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().First(c => c.Name == "no customer has this name"));

            var firstOrDefaultPred = this.CustomersODataQuery().Where(c => c.Name.Contains("ert")).FirstOrDefault(c => c.Name.Contains("A"));
            firstOrDefaultPred.Name.ShouldEqual("Albert");
            var firstOrDefaultPred2 = this.CustomersODataQuery().FirstOrDefault(c => c.Name == "no customer has this name");
            firstOrDefaultPred2.ShouldEqual(null);

            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Last());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Last(c => c.Name == "Dominic"));
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().LastOrDefault());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().LastOrDefault(c => c.Name == "Dominic"));
        }

        [TestMethod]
        public void IntegrationTestDynamicMinAndMax()
        {
            var minDate = CustomersContext.GetCustomers().Min(c => c.DateCreated);
            this.CustomersODataQuery().Select(c => c.DateCreated).Min().ShouldEqual(minDate);
            this.CustomersODataQuery().Min(c => c.DateCreated).ShouldEqual(minDate);
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Length == int.MaxValue).Min(c => c.DateCreated));
            Assert.IsNotNull(this.CustomersODataQuery().Where(c => c.Company != null).Min(c => c.Company.DateClosed));

            var maxDate = CustomersContext.GetCustomers().Max(c => c.DateCreated);
            this.CustomersODataQuery().Select(c => c.DateCreated).Max().ShouldEqual(maxDate);
            this.CustomersODataQuery().Max(c => c.DateCreated).ShouldEqual(maxDate);
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Length == int.MaxValue).Max(c => c.DateCreated));
            Assert.IsNotNull(this.CustomersODataQuery().Where(c => c.Company != null).Max(c => c.Company.DateClosed));
        }

        [TestMethod]
        public void IntegrationTestDynamicSingle()
        {
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Take(0).Single());
            this.CustomersODataQuery().Take(0).SingleOrDefault().ShouldEqual(null);

            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Single(c => c.Name == "no customer has this name"));
            this.CustomersODataQuery().SingleOrDefault(c => c.Name == "no customer has this name").ShouldEqual(null);

            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Contains("A")).Single());
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Contains("A")).SingleOrDefault());

            this.CustomersODataQuery().Where(c => c.Name == "Dominic").Single().Name.ShouldEqual("Dominic");
            this.CustomersODataQuery().Where(c => c.Name == "Dominic").SingleOrDefault().Name.ShouldEqual("Dominic");
            this.CustomersODataQuery().Single(c => c.Name == "Dominic").Name.ShouldEqual("Dominic");
            this.CustomersODataQuery().SingleOrDefault(c => c.Name == "Dominic").Name.ShouldEqual("Dominic");
        }

        [TestMethod]
        public void IntegrationTestDynamicOrDefaultMethodsWithValueTypes()
        {
            var ints = this.CustomersODataQuery().Select(c => c.Name.Length);

            ints.FirstOrDefault(i => i > 2 && i < 2).ShouldEqual(default(int));
            ints.SingleOrDefault(i => i > 2 && i < 2).ShouldEqual(default(int));
        }

        [TestMethod]
        public void IntegrationTestDynamicSumAndAverage()
        {
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Select(c => c.DateCreated.Year).Sum());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Select(c => c.DateCreated.Year).Average());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Sum(c => c.DateCreated.Year));
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Average(c => c.DateCreated.Year));
        }

        [TestMethod]
        public void IntegrationTestDynamicCount()
        {
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1).Take(3).Count().ShouldEqual(3);
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1).Take(3).LongCount().ShouldEqual(3);
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1).Take(3000).Count().ShouldEqual(CustomersContext.GetCustomers().Count - 1);
            this.CustomersODataQuery().Where(c => c.Name.Length % 2 == 1)
                .Count()
                .ShouldEqual(CustomersContext.GetCustomers().Count(c => c.Name.Length % 2 == 1));
            this.CustomersODataQuery().Count(c => c.Name.Length % 2 == 1)
                .ShouldEqual(CustomersContext.GetCustomers().Count(c => c.Name.Length % 2 == 1));
            this.CustomersODataQuery().LongCount(c => c.Name.Length % 2 == 1)
                .ShouldEqual(CustomersContext.GetCustomers().Count(c => c.Name.Length % 2 == 1));
        }

        [TestMethod]
        public void IntegrationTestDynamicAnyAndAll()
        {
            this.CustomersODataQuery().Where(c => c.Name == "Dominic").Any().ShouldEqual(true);
            this.CustomersODataQuery().Where(c => c.Name == "no customer has this name").Any().ShouldEqual(false);
            this.CustomersODataQuery().Any(c => c.Name == "Dominic").ShouldEqual(true);
            this.CustomersODataQuery().Any(c => c.Name == "no customer has this name").ShouldEqual(false);

            this.CustomersODataQuery().Where(c => c.Name.Contains("lbert")).All(c => c.Name.StartsWith("A"))
                .ShouldEqual(true);
            this.CustomersODataQuery().All(c => c.Name.StartsWith("A"))
                .ShouldEqual(false);
        }

        [TestMethod]
        public void IntegrationTestDynamicContains()
        {
            this.CustomersODataQuery().Select(c => c.Name)
                .Contains("no customer has this name")
                .ShouldEqual(false);

            this.CustomersODataQuery().Select(c => c.Name)
                .Contains("Dominic")
                .ShouldEqual(true);

            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Contains(new Customer()));
        }

        [TestMethod]
        public void IntegrationTestDynamicExecuteMethodWithComplexProjection()
        {
            this.CustomersODataQuery().Select(c => c.DateCreated.Day % 5)
                .Min()
                .ShouldEqual(CustomersContext.GetCustomers().Min(c => c.DateCreated.Day % 5));
        }

        [TestMethod]
        public void IntegrationTestDynamicExecuteAsync()
        {
            this.CustomersODataQuery().ExecuteAsync(q => q.Count(c => c.Name.Length % 2 == 1)).Result
                .ShouldEqual(CustomersContext.GetCustomers().Count(c => c.Name.Length % 2 == 1));
        }

        [TestMethod]
        public void IntegrationTestDynamicExecuteQueryAsync()
        {
            var result = this.CustomersODataQuery().Where(c => c.Company != null)
                .OrderBy(c => c.Id)
                .Skip(1)
                .Take(2)
                .ExecuteQueryAsync(new ODataQueryOptions(inlineCount: ODataInlineCountOption.AllPages)).Result;
            result.TotalCount.ShouldEqual(this.CustomersODataQuery().Where(c => c.Company != null).Count());
            result.Results.Select(c => c.Id)
                .CollectionShouldEqual(
                    this.CustomersODataQuery().Where(c => c.Company != null).OrderBy(c => c.Id).Skip(1).Take(2).Select(c => c.Id),
                    orderMatters: true
                );
        }

        [TestMethod]
        public void IntegrationTestDynamicLet()
        {
            var query = (from c in this.CustomersODataQuery()
                         let trimmedName = c.Name.Trim()
                         let isBert = trimmedName.EndsWith("ert")
                         where isBert || !c.CompanyId.HasValue
                         select trimmedName);
            query.CollectionShouldEqual(CustomersContext.GetCustomers().Where(c => c.Name.Trim().EndsWith("ert") || !c.CompanyId.HasValue).Select(c => c.Name.Trim()));
        }

        private IQueryable<Customer> CustomersODataQuery()
        {
            return _provider.Query<Customer>(_testServer.Prefix + "customers");
        }

        private static readonly ODataQueryContext _provider = new ODataQueryContext();
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
