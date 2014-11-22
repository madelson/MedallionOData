using Medallion.OData.Tests.Integration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace Medallion.OData.Samples.Web.Controllers
{
    public class OrdersContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }

        #region ---- Extras ----
        public OrdersContext()
            : base(
                new SqlConnectionStringBuilder
                {
                    DataSource = @".\SqlExpress",
                    InitialCatalog = "OrdersContext",
                    IntegratedSecurity = true,
                    PersistSecurityInfo = true,
                }
                .ConnectionString
            )
        {
            Database.SetInitializer<OrdersContext>(new Initializer());
        }

        public static void Initialize()
        {
            using (var context = new OrdersContext())
            {
                context.Database.Initialize(force: false);
            }
        }

        private class Initializer : DropCreateDatabaseIfModelChanges<OrdersContext>
        {
            protected override void Seed(OrdersContext context)
            {
                var products = new[] { "Wood", "Sheep", "Clay", "Wheat", "Ore", "Road", "Settlement" }
                    .Select(p => new Product { Name = p })
                    .ToArray();
                var customers = new[] { "Blue", "Red", "Purple", "Yellow", "White", "Orange", "Bandit" }
                    .Select(c => new Customer { Name = c })
                    .ToArray();

                var random = new Random(12345);
                var orders = Enumerable.Range(1, 100)
                    .Select(i => new Order { Customer = customers[random.Next(customers.Length)], Product = products[random.Next(products.Length)], Units = random.Next(1, 6), })
                    .ToArray();

                context.Products.AddRange(products);
                context.Customers.AddRange(customers);
                context.Orders.AddRange(orders);
                context.SaveChanges();
            }
        }
        #endregion
    }

    public class Customer
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Product 
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Order
    {
        [Key]
        public int Id { get; set; }
        public int Units { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual Product Product { get; set; }
    }
}