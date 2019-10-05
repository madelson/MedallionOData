using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    /// <summary>
    /// Provides functionality for executing SQL statements and materializing result objects
    /// </summary>
    public abstract class SqlExecutor
    {
        /// <summary>
        /// Executes the given <paramref name="sql"/> query using the given <paramref name="parameters"/>. Results should be materialized
        /// to <paramref name="resultType"/>
        /// </summary>
        protected internal abstract IEnumerable Execute(string sql, IReadOnlyList<Parameter> parameters, Type resultType);
    }

    /// <summary>
    /// Represents a parameter in a SQL query. Similar to <see cref="DbParameter"/>, but not abstract
    /// </summary>
    public sealed class Parameter
    {
        internal Parameter(string name, Type type, object value)
        {
            Throw.If(string.IsNullOrEmpty(name), "name");
            Throw.IfNull(type, "type");

            this.Name = name;
            this.Type = type;
            this.Value = value;
        }

        /// <summary>
        /// The parameter name
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The CLR parameter type
        /// </summary>
        public Type Type { get; private set; }
        /// <summary>
        /// The parameter value
        /// </summary>
        public object Value { get; private set; }
    }
}
