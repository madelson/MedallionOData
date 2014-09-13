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
        [ClassInitialize]
        public static void CreateDatabase()
        {
            using (var context = new CustomersContext())
            {
                context.Customers.Any().ShouldEqual(true);
            }
        }

        protected abstract ODataSqlContext Context { get; }

        [TestMethod]
        public void SqlReadNames()
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
    }
}
