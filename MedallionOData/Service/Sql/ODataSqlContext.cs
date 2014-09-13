using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    /// <summary>
    /// Provides an entry point for creating OData compatible <see cref="IQueryable"/>s on top of SQL
    /// </summary>
    public sealed class ODataSqlContext
    {
        private readonly DatabaseProvider provider;

        /// <summary>
        /// Creates a context that uses the given <see cref="DatabaseProvider"/>
        /// </summary>
        public ODataSqlContext(DatabaseProvider provider)
        {
            Throw.IfNull(provider, "provider");

            this.provider = provider;
        }

        /// <summary>
        /// Creates a query which reads from <paramref name="tableSql"/>. When executed, the query will
        /// run something like "SELECT * FROM [tableSql] t"
        /// </summary>
        public IQueryable<T> Query<T>(string tableSql)
        {
            Throw.If(string.IsNullOrWhiteSpace(tableSql), "tableSql is required");

            return new SqlQueryProviderForOData<T>(tableSql, this.provider);
        }
    }
}
