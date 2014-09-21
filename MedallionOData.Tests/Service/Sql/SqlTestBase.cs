using Medallion.OData.Client;
using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service.Sql
{
    public abstract class SqlTestBase
    {
        protected abstract ODataSqlContext Context { get; }

        [TestMethod]
        public void SqlSimpleSelect()
        {
            var names = this.Context.Query<Customer>("customers").Select(c => c.Name).ToArray();
            var dynamicNames = this.Context.Query<ODataEntity>("customers").Select(c => c.Get<string>("Name")).ToArray();
            using (var context = new CustomersContext())
            {
                var expectedNames = context.Customers.Select(c => c.Name).ToArray();
                names.CollectionShouldEqual(expectedNames);
                dynamicNames.CollectionShouldEqual(expectedNames);
            }
        }

        [TestMethod]
        public void SqlFilter()
        {
            var filtered = this.Context.Query<Customer>("customers")
                .Where(c => c.Name.Contains("b") || c.Salary > 100);
            var dynamicFiltered = this.Context.Query<ODataEntity>("customers")
                .Where(c => c.Get<string>("Name").Contains("b") || c.Get<double>("Salary") > 100);
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.Where(c => c.Name.Contains("b") || c.Salary > 100);
                filtered.CollectionShouldEqual(expected);
                dynamicFiltered.Select(c => c.Get<Guid>("Id")).CollectionShouldEqual(expected.Select(c => c.Id));
            }
        }

        [TestMethod]
        public void SqlSort()
        {
            var sorted = this.Context.Query<Customer>("customers")
                .OrderBy(c => c.Salary)
                .ThenBy(c => c.Name.Length)
                .ThenByDescending(c => c.Name)
                .ToArray();
            var dynamicSorted = this.Context.Query<ODataEntity>("customers")
                .OrderBy(c => c.Get<double>("Salary"))
                .ThenBy(c => c.Get<string>("Name").Length)
                .ThenByDescending(c => c.Get<string>("Name"))
                .ToArray();
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.OrderBy(c => c.Salary)
                    .ThenBy(c => c.Name.Length)
                    .ThenByDescending(c => c.Name)
                    .ToArray();
                sorted.CollectionShouldEqual(expected, orderMatters: true);
                dynamicSorted.Select(c => c.Get<string>("Name")).CollectionShouldEqual(expected.Select(c => c.Name), orderMatters: true);
            }
        }

        [TestMethod]
        public void SqlPaginate()
        {
            var sorted = this.Context.Query<Customer>("customers").OrderByDescending(c => c.Name.Length)
                .ThenBy(c => c.Name);
            var dynamicSorted = this.Context.Query<ODataEntity>("customers").OrderByDescending(c => c.Get<string>("Name").Length)
                .ThenBy(c => c.Get<string>("Name"))
                .Select(c => c.Get<Guid>("Id"));
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.OrderByDescending(c => c.Name.Length)
                    .ThenBy(c => c.Name);
                sorted.Skip(3).Take(1).CollectionShouldEqual(expected.Skip(3).Take(1));
                dynamicSorted.Skip(1).Take(3).CollectionShouldEqual(expected.Select(c => c.Id).Skip(1).Take(3), orderMatters: true);
                sorted.Take(2).CollectionShouldEqual(expected.Take(2), orderMatters: true);
                dynamicSorted.Skip(2).CollectionShouldEqual(expected.Select(c => c.Id).Skip(2), orderMatters: true);

                this.Context.Query<Sample>("samples").Take(10).ToArray().Count().ShouldEqual(10, "unsorted take");
                this.Context.Query<Sample>("samples").Skip(3).ToArray().Count().ShouldEqual(context.Samples.Count() - 3, "unsorted skip");
            }

            this.Context.Query<Customer>("customers").Take(0).Count().ShouldEqual(0, "top 0");
        }

        [TestMethod]
        public void SqlNullableEquality()
        {
            var samples = this.Context.Query<Sample>("samples");
            var dynamicSamples = this.Context.Query<ODataEntity>("samples");

            var nulls = samples.Where(s => s.NullableBool == null);
            var dynamicNulls = dynamicSamples.Where(s => s.Get<bool>("NullableBool") == null);
            var nonNulls = samples.Where(s => s.NullableBool != null);
            var dynamicNonNulls = dynamicSamples.Where(s => s.Get<bool?>("NullableBool") != null);
            var negatedNulls = samples.Where(s => !(s.NullableBool == null));
            var negatedDynamicNulls = dynamicSamples.Where(s => !(s.Get<bool?>("NullableBool") == null));
            var negatedNonNulls = samples.Where(s => !(s.NullableBool != null));
            var negatedDynamicNonNulls = dynamicSamples.Where(s => !(s.Get<bool?>("NullableBool") != null)); 

            using (var context = new CustomersContext())
            {
                var efNulls = context.Samples.Where(s => s.NullableBool == null).ToArray();
                nulls.CollectionShouldEqual(efNulls, "nulls");
                negatedNonNulls.CollectionShouldEqual(efNulls, "negatedNonNulls");
                dynamicNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNulls.Select(s => s.Id), "dynamicNulls");
                negatedDynamicNonNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNulls.Select(s => s.Id), "negatedDynamicNonNulls");
            }

            using (var context = new CustomersContext())
            {
                var efNonNulls = context.Samples.Where(s => s.NullableBool != null).ToArray();
                nonNulls.CollectionShouldEqual(efNonNulls, "nonNulls");
                negatedNulls.CollectionShouldEqual(efNonNulls, "negatedNulls");
                dynamicNonNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNonNulls.Select(s => s.Id), "dynamicNonNulls");
                negatedDynamicNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNonNulls.Select(s => s.Id), "negatedDynamicNulls");
            }
        }

        [TestMethod]
        public void SqlToString()
        {
            var query = this.Context.Query<Sample>("fake_table")
                .Where(s => s.Id % 2 == 0);
            var queryString = query.ToString();
            Console.WriteLine(queryString);
            queryString.Contains("fake_table").ShouldEqual(true);

            var badQuery = this.Context.Query<Sample>("samples").Where(s => s.GetHashCode() > 5);
            var badQueryString = badQuery.ToString();
            Console.WriteLine(badQueryString);
            badQueryString.Contains("Exception").ShouldEqual(true);
        }

        [TestMethod]
        public void SqlBitBoolConfusion()
        {
            using (var context = new CustomersContext())
            {
                this.Context.Query<Sample>("samples")
                    .Where(s => (s.Bool == false) == (s.Id % 3 == 0))
                    .CollectionShouldEqual(context.Samples.Where(s => (s.Bool == false) == (s.Id % 3 == 0)));

                this.Context.Query<Sample>("samples")
                    .Where(s => !s.Bool || !!(false == s.Bool))
                    .CollectionShouldEqual(context.Samples.Where(s => !s.Bool || !!(false == s.Bool)));

                this.Context.Query<Sample>("samples")
                    .Select(s => s.Bool && (s.Id > 1))
                    .CollectionShouldEqual(context.Samples.Select(s => s.Bool && (s.Id > 1)));

                this.Context.Query<Sample>("samples")
                    .Where(s => s.Bool)
                    .CollectionShouldEqual(context.Samples.Where(s => s.Bool));

                this.Context.Query<ODataEntity>("samples")
                    .OrderByDescending(s => s.Get<bool>("Bool"))
                    .ToArray()
                    .Select(s => s.Get<int>("Id"))
                    .CollectionShouldEqual(context.Samples.OrderByDescending(s => s.Bool).Select(s => s.Id), orderMatters: true);
            }
        }

        [TestMethod]
        public void SqlTestStartsWithAndEndsWith()
        {
            this.Context.Query<Customer>("customers")
                .Where(c => c.Name.StartsWith("A"))
                .Select(c => c.Name)
                .CollectionShouldEqual(new[] { "A", "Albert" });

            this.Context.Query<ODataEntity>("customers")
                .Select(c => c.Get<string>("Name"))
                .Where(n => n.EndsWith("ert"))
                .CollectionShouldEqual(new[] { "Bert", "Albert" });
        }

        [TestMethod]
        public void SqlCount()
        {
            using (var context = new CustomersContext())
            {
                this.Context.Query<Sample>("samples").Count()
                    .ShouldEqual(context.Samples.Count());
                this.Context.Query<ODataEntity>("samples").Count(s => s.Get<bool>("Bool"))
                    .ShouldEqual(context.Samples.Count(s => s.Bool));
                this.Context.Query<Sample>("samples").OrderBy(s => s.Id).Skip(1).Count()
                    .ShouldEqual(context.Samples.OrderBy(s => s.Id).Skip(1).Count()); 

                foreach (var val in new[] { 3, 300000 })
                {
                    this.Context.Query<Sample>("samples").Take(val).Count()
                        .ShouldEqual(context.Samples.Take(val).Count());

                    this.Context.Query<ODataEntity>("samples").OrderByDescending(s => s.Get<int>("Id")).Skip(val).Count()
                        .ShouldEqual(context.Samples.OrderByDescending(s => s.Id).Skip(val).Count());
                }
            }
        }

        [TestMethod]
        public void SqlErrors()
        {
            var samples = this.Context.Query<Sample>("samples");
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => samples.Join(samples, s => s.Id, s => s.Id, (s1, s2) => s1.Id + s2.Id).ToArray());
            UnitTestHelpers.AssertThrows<DbException>(() => this.Context.Query<Sample>("fake_samples").ToArray());
            UnitTestHelpers.AssertThrows<NotSupportedException>(
                () => this.Context.Query<Customer>("customers").Where(c => c.Company.Name.Contains("e")).ToArray()
            );
            // see note in ODataToSqlTranslator.VisitMemberAccess about why we can't throw NotSupported here
            UnitTestHelpers.AssertThrows<DbException>(
                () => this.Context.Query<ODataEntity>("customers").Where(c => c.Get<ODataEntity>("company") != null).ToArray()
            );
        }
    }
}
