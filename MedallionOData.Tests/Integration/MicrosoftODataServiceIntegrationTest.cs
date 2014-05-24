using Medallion.OData.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Integration
{
    [TestClass]
    public class MicrosoftODataServiceIntegrationTest
    {
        private ODataQueryContext context = new ODataQueryContext();
        private const string BaseUrl = @"http://services.odata.org/v3/odata/odata.svc/";

        [TestMethod]
        public void TestCategories()
        {
            var categories = context.Query<Category>(BaseUrl + "Categories");

            var food = categories.Where(c => c.Name == "Food")
                .Select(c => c.ID)
                .Single()
                .ShouldEqual(0);
        }

        [TestMethod]
        public void TestProducts()
        {
            var products = context.Query<ODataEntity>(BaseUrl + "Products");

            var cranberries = products.Single(p => p.Get<string>("Name") == "Cranberry Juice");
            cranberries.Get<DateTime>("ReleaseDate").Year.ShouldEqual(2006);

            var expensiveNovemberProducts = context.Query<ODataEntity>(BaseUrl + "Products")
                .Where(p => p.Get<double>("Price") >= 20)
                .Where(p => p.Get<DateTime>("ReleaseDate").Month == 11)
                .Select(p => p.Get<string>("Name"))
                .OrderBy(p => p)
                .ToArray();
            expensiveNovemberProducts.CollectionShouldEqual(new[] { "DVD Player" });
        }

        private class Category
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }
    }
}
