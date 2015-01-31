using Medallion.OData.Client;
using Medallion.OData.Service;
using Medallion.OData.Trees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Tests.Integration
{
    public abstract class IntegrationTestBase<TTest>
        where TTest : IntegrationTestBase<TTest>, new()
    {
        #region ---- Test Server ----
        protected abstract TestServer CreateTestServer();

        private static readonly Lazy<TestServer> testServer = new Lazy<TestServer>(() => new TTest().CreateTestServer());

        protected static void DisposeTestServer()
        {
            if (testServer.IsValueCreated)
            {
                testServer.Value.As<IDisposable>().Dispose();
            }
        }
        #endregion

        #region ---- Configuration ----
        protected virtual bool AssociationsSupported { get { return true; } }
        protected virtual bool NullCoalescingSupported { get { return true; } }
        protected virtual bool FullNumericMixupSupported { get { return true; } }
        #endregion

        #region ---- Test Cases ----
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
            if (this.AssociationsSupported)
            {
                this.Test(
                    "customers",
                    (IQueryable<Customer> cc) => cc.Select(c => new { c.Company, c.Name }),
                    expected: CustomersContext.GetCustomers().Select(c => new { c.Company, c.Name })
                );
            }
        }

        [TestMethod]
        public void IntegrationDynamicRowFilterAndProjectToSimpleType()
        {
            if (this.AssociationsSupported)
            {
                this.Test(
                    "customers",
                    (IQueryable<ODataEntity> rows) => rows.Where(
                            this.NullCoalescingSupported
                                ? (Expression<Func<ODataEntity, bool>>)(c => c.Get<ODataEntity>("Company").Get<string>("Name") == "Mine")
                                : c => c.Get<ODataEntity>("Company") != null && c.Get<ODataEntity>("Company").Get<string>("Name") == "Mine"
                        )
                        .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.Company != null && c.Company.Name == "Mine")
                        .Select(c => c.Name)
                );
            }
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

            this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<double>("AwardCount") > 4.7)
                    .Select(c => c.Get<string>("Name")),
                expected: CustomersContext.GetCustomers().Where(c => c.AwardCount > 4.7)
                        .Select(c => c.Name)
            );

            // we sometimes can't do this test because in-memory ODataEntity will have double as salary, but the other
            // side will guess int after seeing "Salary gt 50000"
            if (this.FullNumericMixupSupported)
            {
                this.Test(
                    "customers",
                    (IQueryable<ODataEntity> rows) => rows.Where(c => c.Get<double?>("Salary") > 50000)
                        .Select(c => c.Get<string>("Name")),
                    expected: CustomersContext.GetCustomers().Where(c => c.Salary > 50000)
                            .Select(c => c.Name)
                );
            }
        }

        [TestMethod]
        public void IntegrationDynamicRowComplexQuery()
        {
            if (this.AssociationsSupported)
            {
                this.Test(
                "customers",
                (IQueryable<ODataEntity> rows) =>
                    this.NullCoalescingSupported
                        ? rows.Select(r => new { b = r.Get<ODataEntity>("Company"), c = r.Get<string>("Name").Length * 2 })
                            .Where(t => t.b.Get<string>("Name").Length % 3 != t.c % 3)
                            .Select(t => t.c)
                        : rows.Select(r => new { b = r.Get<ODataEntity>("Company"), c = r.Get<string>("Name").Length * 2 })
                            .Where(t => t.b == null || t.b.Get<string>("Name").Length % 3 != t.c % 3)
                            .Select(t => t.c),
                    expected: CustomersContext.GetCustomers().Select(c => new { b = c.Company, c = c.Name.Length * 2 })
                        // note: we need == null here because C# nullability semantics do not match EF's 
                        .Where(t => t.b == null || t.b.Name.Length % 3 != t.c % 3)
                        .Select(t => t.c)
                );
            }
        }

        [TestMethod]
        public void IntegrationFilterByYear()
        {
            if (this.AssociationsSupported)
            {
                this.Test<Customer, Customer>(
                    "customers",
                    cc => cc.Where(c => c.Company != null && c.Company.DateClosed.HasValue && ((DateTime)c.Company.DateClosed).Year == 1988),
                    expected: CustomersContext.GetCustomers().Where(c => c.Company != null && c.Company.DateClosed.HasValue && ((DateTime)c.Company.DateClosed).Year == 1988)
                );
            }
        }

        [TestMethod]
        public void IntegrationTestFirstAndLast()
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
        public void IntegrationTestMinAndMax()
        {
            Func<IQueryable<Customer>, IQueryable<Customer>> companyFilter = this.NullCoalescingSupported 
                    ? new Func<IQueryable<Customer>, IQueryable<Customer>>(q => q)
                    : q => q.Where(c => c.Company != null);

            var minDate = CustomersContext.GetCustomers().Min(c => c.DateCreated);
            this.CustomersODataQuery().Select(c => c.DateCreated).Min().ShouldEqual(minDate);
            this.CustomersODataQuery().Min(c => c.DateCreated).ShouldEqual(minDate);
            // this check is because we're doing min(valueType) of an empty list
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Length == int.MaxValue).Min(c => c.DateCreated));
            if (this.AssociationsSupported)
            {
                Assert.IsNotNull(companyFilter(this.CustomersODataQuery()).Min(c => c.Company.DateClosed));
            }

            var maxDate = CustomersContext.GetCustomers().Max(c => c.DateCreated);
            this.CustomersODataQuery().Select(c => c.DateCreated).Max().ShouldEqual(maxDate);
            this.CustomersODataQuery().Max(c => c.DateCreated).ShouldEqual(maxDate);
            // this check is because we're doing max(valueType) of an empty list
            UnitTestHelpers.AssertThrows<InvalidOperationException>(() => this.CustomersODataQuery().Where(c => c.Name.Length == int.MaxValue).Max(c => c.DateCreated));
            if (this.AssociationsSupported)
            {
                Assert.IsNotNull(companyFilter(this.CustomersODataQuery()).Max(c => c.Company.DateClosed));
            }
        }

        [TestMethod]
        public void IntegrationTestSingle()
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
        public void IntegrationTestOrDefaultMethodsWithValueTypes()
        {
            var ints = this.CustomersODataQuery().Select(c => c.Name.Length);

            ints.FirstOrDefault(i => i > 2 && i < 2).ShouldEqual(default(int));
            ints.SingleOrDefault(i => i > 2 && i < 2).ShouldEqual(default(int));
        }

        [TestMethod]
        public void IntegrationTestSumAndAverage()
        {
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Select(c => c.DateCreated.Year).Sum());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Select(c => c.DateCreated.Year).Average());
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Sum(c => c.DateCreated.Year));
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.CustomersODataQuery().Average(c => c.DateCreated.Year));
        }

        [TestMethod]
        public void IntegrationTestCount()
        {
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1).Take(3).Count().ShouldEqual(3);
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1).Take(3).LongCount().ShouldEqual(3);
            this.CustomersODataQuery().OrderBy(c => c.Id).Take(0).Count().ShouldEqual(0);
            this.CustomersODataQuery().OrderBy(c => c.Id).Take(0).LongCount().ShouldEqual(0);
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1000000).Count().ShouldEqual(0);
            this.CustomersODataQuery().OrderBy(c => c.Id).Skip(1000000).LongCount().ShouldEqual(0);
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
        public void IntegrationTestAnyAndAll()
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
        public void IntegrationTestContains()
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
        public void IntegrationTestExecuteMethodWithComplexProjection()
        {
            this.CustomersODataQuery().Select(c => c.DateCreated.Day % 5)
                .Min()
                .ShouldEqual(CustomersContext.GetCustomers().Min(c => c.DateCreated.Day % 5));
        }

        [TestMethod]
        public void IntegrationTestExecuteAsync()
        {
            this.CustomersODataQuery().ExecuteAsync(q => q.Count(c => c.Name.Length % 2 == 1)).Result
                .ShouldEqual(CustomersContext.GetCustomers().Count(c => c.Name.Length % 2 == 1));
        }

        [TestMethod]
        public void IntegrationTestExecuteQueryAsync()
        {
            Expression<Func<Customer, bool>> filter;
            if (this.AssociationsSupported)
            {
                filter = c => c.Company != null;
            }
            else
            {
                filter = c => c.AwardCount != 0;
            }

            var result = this.CustomersODataQuery().Where(filter)
                .OrderBy(c => c.Id)
                .Skip(1)
                .Take(2)
                .ExecuteQueryAsync(new ODataQueryOptions(inlineCount: ODataInlineCountOption.AllPages)).Result;
            result.TotalCount.ShouldEqual(this.CustomersODataQuery().Where(filter).Count());
            result.Results.Select(c => c.Id)
                .CollectionShouldEqual(
                    this.CustomersODataQuery().Where(filter).OrderBy(c => c.Id).Skip(1).Take(2).Select(c => c.Id),
                    orderMatters: true
                );
        }

        [TestMethod]
        public void IntegrationTestLet()
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
            return _provider.Query<Customer>(testServer.Value.Prefix + "customers");
        }

        private static readonly ODataQueryContext _provider = new ODataQueryContext();
        private void Test<TSource, TResult>(string url, Func<IQueryable<TSource>, IQueryable<TResult>> query, IEnumerable<TResult> expected, IEqualityComparer<TResult> comparer = null, bool orderMatters = false)
        {
            var uri = new Uri(testServer.Value.Prefix + url);
            var rootQuery = _provider.Query<TSource>(uri);
            var resultQuery = query(rootQuery);
            var result = resultQuery.ToArray();
            result.CollectionShouldEqual(expected, orderMatters: orderMatters, comparer: comparer);
        }

        [TestMethod]
        public void IntegrationTestRelativeUri()
        {
            var provider = new ODataQueryContext(new RelativeUriPipeline());
            provider.Query<Customer>("/customers")
                .Single(c => c.Name == "Albert")
                .Id
                .ShouldEqual(CustomersContext.GetCustomers().Single(c => c.Name == "Albert").Id);

            provider.Query<Customer>(new Uri("/customers", UriKind.Relative))
                .Single(c => c.Name == "Albert")
                .Id
                .ShouldEqual(CustomersContext.GetCustomers().Single(c => c.Name == "Albert").Id);
        }

        private class RelativeUriPipeline : IODataClientQueryPipeline
        {
            private readonly IODataClientQueryPipeline _pipeline = new DefaultODataClientQueryPipeline();

            IODataTranslationResult IODataClientQueryPipeline.Translate(System.Linq.Expressions.Expression expression, ODataQueryOptions options)
            {
                return this._pipeline.Translate(expression, options);
            }

            Task<IODataWebResponse> IODataClientQueryPipeline.ReadAsync(Uri url)
            {
                Assert.IsFalse(url.IsAbsoluteUri, "relative uri expected!");
                var finalUri = new Uri(new Uri(testServer.Value.Prefix), url.ToString().TrimStart('/'));
                return this._pipeline.ReadAsync(finalUri);
            }

            Task<IODataDeserializationResult> IODataClientQueryPipeline.DeserializeAsync(IODataTranslationResult translation, System.IO.Stream response)
            {
                return this._pipeline.DeserializeAsync(translation, response);
            }
        }
        #endregion
    }
}
