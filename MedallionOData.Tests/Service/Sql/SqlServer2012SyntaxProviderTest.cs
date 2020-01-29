using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using NUnit.Framework;

namespace Medallion.OData.Tests.Service.Sql
{
    public class SqlServer2012SyntaxProviderTest : SqlTestBase
    {
        private readonly Lazy<ODataSqlContext> context = new Lazy<ODataSqlContext>(
            () =>
            {
                using (var efContext = new CustomersContext())
                {
                    efContext.Database.Initialize(force: false);
                    var connectionString = efContext.Database.Connection.ConnectionString;
                    return new ODataSqlContext(new SqlServerSyntax(SqlServerSyntax.Version.Sql2012), new DefaultSqlExecutor(() => new SqlConnection(connectionString)));
                }
            }
        );
        protected override ODataSqlContext Context
        {
            get { return this.context.Value; }
        }

        [Test]
        public void SqlGetVersion([Values] bool useSystemDataSqlClient)
        {
            using (var efContext = new CustomersContext())
            {
                efContext.Database.Initialize(force: false);
                using (var connection = useSystemDataSqlClient
                    ? new SqlConnection(efContext.Database.Connection.ConnectionString).As<IDbConnection>()
                    : new Microsoft.Data.SqlClient.SqlConnection(efContext.Database.Connection.ConnectionString))
                {
                    SqlServerSyntax.GetVersion(connection).ShouldEqual(SqlServerSyntax.Version.Sql2012);
                }
            }
        }
    }
}
