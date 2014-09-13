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
    [TestClass]
    public class SqlDatabaseProviderTest : SqlTestBase
    {
        private readonly Lazy<ODataSqlContext> context = new Lazy<ODataSqlContext>(() =>
        {
            using (var efContext = new CustomersContext())
            {
                var connectionString = efContext.Database.Connection.ConnectionString;
                return new ODataSqlContext(new SqlDatabaseProvider(() => connectionString));
            }
        });
        protected override ODataSqlContext Context
        {
            get { return this.context.Value; }
        }
    }
}
