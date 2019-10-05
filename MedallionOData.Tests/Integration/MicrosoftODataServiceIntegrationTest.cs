using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using NUnit.Framework;

namespace Medallion.OData.Tests.Integration
{
    public class MicrosoftODataServiceIntegrationTest
    {
        private const string BaseUrl = @"http://services.odata.org/v3/odata/odata.svc/";
        private static readonly ODataQueryContext Context = new ODataQueryContext();
        
        [Test]
        public void TestCategories()
        {
            var categories = Context.Query<Category>(BaseUrl + "Categories");

            var food = categories.Where(c => c.Name == "Food")
                .Select(c => c.ID)
                .Single()
                .ShouldEqual(0);
        }

        [Test]
        public void TestProducts()
        {
            var products = Context.Query<ODataEntity>(BaseUrl + "Products");

            var cranberries = products.Single(p => p.Get<string>("Name") == "Cranberry Juice");
            cranberries.Get<DateTime>("ReleaseDate").Year.ShouldEqual(2006);

            var expensiveNovemberProducts = Context.Query<ODataEntity>(BaseUrl + "Products")
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
