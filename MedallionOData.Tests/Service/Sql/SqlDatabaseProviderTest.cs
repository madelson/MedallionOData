using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service.Sql
{
    [TestClass]
    public class SqlSyntaxProviderTest : SqlTestBase
    {
        private readonly Lazy<ODataSqlContext> context = new Lazy<ODataSqlContext>(
            () =>
            {
                using (var efContext = new CustomersContext())
                {
                    efContext.Database.Initialize(force: false); // needed so we can call GetVersion

                    var connectionString = efContext.Database.Connection.ConnectionString;
                    using (var connection = new SqlConnection(connectionString))
                    {
                        return new ODataSqlContext(new SqlServerSyntax(SqlServerSyntax.GetVersion(connection)), new DefaultSqlExecutor(() => new SqlConnection(connectionString)));
                    }
                }
            }
        );
        protected override ODataSqlContext Context
        {
            get { return this.context.Value; }
        }
    }
}
