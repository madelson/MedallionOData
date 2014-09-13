using Medallion.OData.Client;
using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
            }
        }
    }
}
