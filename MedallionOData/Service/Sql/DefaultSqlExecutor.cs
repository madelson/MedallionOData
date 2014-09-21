using Medallion.OData.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    /// <summary>
    /// Provides a default implementation of <see cref="SqlExecutor"/> using a <see cref="DbConnection"/>
    /// </summary>
    public class DefaultSqlExecutor : SqlExecutor
    {
        private readonly Func<DbConnection> connectionFactory;
        private readonly bool executorOwnsConnection;

        /// <summary>
        /// Creates a <see cref="DefaultSqlExecutor"/> using connections from <param name="connectionFactory"/>. The created
        /// connections will be disposed if and only if <param name="executorOwnsConnection"/>
        /// </summary>
        public DefaultSqlExecutor(Func<DbConnection> connectionFactory, bool executorOwnsConnection = true)
        {
            Throw.IfNull(connectionFactory, "connectionFactory");
            
            this.connectionFactory = connectionFactory;
            this.executorOwnsConnection = executorOwnsConnection;
        }

        /// <summary>
        /// Materializes the given <param name="reader"/> to an <see cref="IEnumerable"/> of the given <param name="resultType"/>
        /// </summary>
        protected internal virtual IEnumerable MaterializeReader(DbDataReader reader, Type resultType)
        {
            Func<DbDataReader, object> materialize;
            if (resultType == typeof(ODataEntity))
            {
                var columnNames = reader.GetSchemaTable().Rows.Cast<DataRow>()
                    .OrderBy(r => r.Field<int>("ColumnOrdinal"))
                    .Select(r => r.Field<string>("ColumnName"))
                    .ToArray();
                materialize = r =>
                {
                    var kvpArray = new KeyValuePair<string, object>[columnNames.Length];
                    for (var i = 0; i < kvpArray.Length; ++i)
                    {
                        kvpArray[i] = KeyValuePair.Create(columnNames[i], r.IsDBNull(i) ? null : r.GetValue(i));
                    }
                    return new ODataEntity(kvpArray);
                };
            }
            else if (resultType == typeof(int))
            {
                // to support count
                materialize = r => r.GetInt32(0);
            }
            else if (resultType.GetConstructor(Type.EmptyTypes) != null)
            {
                // TODO FUTURE offer faster compiled materialization (should optimize for cold start and cache, though!)
                var ordinalPropertyMapping = reader.GetSchemaTable().Rows.Cast<DataRow>()
                    .Select(row => KeyValuePair.Create(row.Field<int>("ColumnOrdinal"), resultType.GetProperty(row.Field<string>("ColumnName"), BindingFlags.Public | BindingFlags.Instance)))
                    .Where(kvp => kvp.Value != null)
                    .ToArray();
                materialize = r =>
                {
                    var obj = Activator.CreateInstance(resultType);
                    for (var i = 0; i < ordinalPropertyMapping.Length; ++i)
                    {
                        var propMapping = ordinalPropertyMapping[i];
                        var value = r.IsDBNull(propMapping.Key) ? null : r.GetValue(propMapping.Key);
                        propMapping.Value.SetValue(obj, value);
                    }
                    return obj;
                };
            }
            else
            {
                throw new NotSupportedException("Unable to materialize type " + resultType + ": it lacks a default constructor and is not " + typeof(ODataEntity));
            }

            while (reader.Read())
            {
                yield return materialize(reader);
            }
        }

        /// <summary>
        /// Implements <see cref="SqlExecutor.Execute"/> using <see cref="MaterializeReader"/>
        /// </summary>
        protected internal sealed override IEnumerable Execute(string sql, IReadOnlyList<Parameter> parameters, Type resultType)
        {
            using (var connection = this.GetConnection())
            using (var command = this.CreateCommand(connection.Connection, sql, parameters))
            using (var reader = command.ExecuteReader())
            {
                foreach (var result in this.MaterializeReader(reader, resultType))
                {
                    yield return result;
                }
            }
        }

        private DbCommand CreateCommand(DbConnection connection, string sql, IReadOnlyList<Parameter> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();
                this.PopulateParameter(dbParameter, parameter);
                command.Parameters.Add(dbParameter);
            }
            return command;
        }

        /// <summary>
        /// Populates the <see cref="DbParameter"/> from the given <see cref="Parameter"/>
        /// </summary>
        protected internal virtual void PopulateParameter(DbParameter dbParameter, Parameter parameter)
        {
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
        }

        private ConnectionHelper GetConnection()
        {
            var connection = this.connectionFactory();
            Throw<InvalidOperationException>.If(connection == null, "The connection generated by the factory was null");
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }
            return new ConnectionHelper(connection, this.executorOwnsConnection);
        }

        private class ConnectionHelper : IDisposable
        {
            private readonly bool executorOwnsConnection;

            public ConnectionHelper(DbConnection connection, bool ownsConnection)
            {
                this.Connection = connection;
                this.executorOwnsConnection = ownsConnection;
            }

            public DbConnection Connection { get; private set; }

            public void Dispose()
            {
                var connection = this.Connection;
                if (connection != null)
                {
                    if (this.executorOwnsConnection)
                    {
                        connection.Dispose();
                    }
                    this.Connection = null;
                }
            }
        }
    }
}
