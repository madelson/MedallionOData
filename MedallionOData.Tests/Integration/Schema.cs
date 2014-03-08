using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Integration
{
    public class Customer
    {
        public Customer() 
        {
            this.Id = Guid.NewGuid();
            this.DateCreated = DateTime.Now;
        }

        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTime DateCreated { get; set; }

        public Guid? CompanyId { get; set; }
        [ForeignKey("CompanyId")]
        public virtual Company Company { get; set; }

        public override bool Equals(object obj)
        {
            var that = obj as Customer;
            return that != null
                && that.Id == this.Id
                && that.Name == this.Name
                && (that.DateCreated - this.DateCreated).Duration() < TimeSpan.FromMilliseconds(10)
                && that.CompanyId == this.CompanyId;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }

    public class Company
    {
        public Company()
        {
            this.Id = Guid.NewGuid();
            this.DateCreated = DateTime.Now;
        }

        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTime DateCreated { get; set; }

        public override bool Equals(object obj)
        {
            var that = obj as Company;
            return that != null
                && this.Id == that.Id
                && this.Name == that.Name
                && (this.DateCreated - that.DateCreated).Duration() < TimeSpan.FromMilliseconds(10);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }

    public class CustomersContext : DbContext
    {
        // TODO instead of caching this in memory, read it back on command to sync date values (or insert reasonable date values)
        private static IReadOnlyList<Customer> _customers;
        public static IReadOnlyList<Customer> GetCustomers()
        {
            var mine = new Company { Name = "Mine" };
            var farm = new Company { Name = "Farm" };
            var customers = new[]
            {
                new Customer { Name = "Albert", Company = mine },
                new Customer { Name = "Bert", Company = mine },
                new Customer { Name = "Catherine", Company = farm },
                new Customer { Name = "Dominic", Company = farm },
                new Customer { Name = "Ethel", Company = farm },
                new Customer { Name = "Fred" },
                new Customer { Name = "A" },
            };
            Interlocked.CompareExchange(ref _customers, value: customers, comparand: null);
            return _customers;
        }

        public CustomersContext()
            : base(GetConnectionString())
        {
            Database.SetInitializer(new Initializer());
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Company> Companies { get; set; }

        private const string Prefix = "Medallion_OData_Tests";
        public static string GetConnectionString()
        {
            var connectionString = new SqlConnectionStringBuilder()
            {
                DataSource = @".\SqlExpress",
                InitialCatalog = Prefix + "_" + typeof(CustomersContext).Name + Math.Round((DateTime.MaxValue - DateTime.Now).TotalSeconds),
                IntegratedSecurity = true,
                PersistSecurityInfo = true,
            };
            return connectionString.ConnectionString;
        }

        protected override void Dispose(bool disposing)
        {
            // delete old databases with the prefix
            var databasesToDelete = this.Database.SqlQuery<string>(@"
                    SELECT name FROM sys.databases
                    WHERE name LIKE '" + Prefix + @"%'
                        AND DATEADD(d, -1, GETDATE()) > create_date"
                )
                .ToArray();

            foreach (var db in databasesToDelete)
            {
                try
                {
                    // needs a separate connection to not 
                    using (var connection = new SqlConnection(this.Database.Connection.ConnectionString))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "DROP DATABASE " + db;
                            command.ExecuteNonQuery();
                        }
                    }
                    Console.WriteLine("Dropped {0}", db);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to drop database {0} ({1})", db, ex.Message);
                }
            }

            base.Dispose(disposing);
        }

        private class Initializer : DropCreateDatabaseAlways<CustomersContext>
        {
            protected override void Seed(CustomersContext context)
            {
                context.Customers.AddRange(GetCustomers());
                context.SaveChanges();
                base.Seed(context);
            }
        }
    }
}
