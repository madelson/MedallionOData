using Medallion.OData.Client;
using Medallion.OData.Service;
using Medallion.OData.Service.Sql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Tests.Integration
{
    [TestClass]
    public class SqlODataEntityIntegrationTest : IntegrationTestBase<SqlODataEntityIntegrationTest>
    {
        protected override TestServer CreateTestServer()
        {
            string connectionString;
            using (var context = new CustomersContext())
            {
                context.Database.Initialize(force: false);
                connectionString = context.Database.Connection.ConnectionString;
            }
            SqlSyntax syntax;
            using (var connection = new SqlConnection(connectionString))
            {
                syntax = new SqlServerSyntax(SqlServerSyntax.GetVersion(connection));
            }
            var sqlContext = new ODataSqlContext(syntax, new DefaultSqlExecutor(() => new SqlConnection(connectionString)));

            var service = new ODataService();
            return new TestServer(url =>
            {
                var query = sqlContext.Query<ODataEntity>("(SELECT * FROM customers)");
                var result = service.Execute(query, HttpUtility.ParseQueryString(url.Query));
                return result.Results.ToString();
            });
        }

        [ClassCleanup]
        public static void TearDown()
        {
            DisposeTestServer();
        }

        protected override bool AssociationsSupported { get { return false; } }
    }
}
