using Medallion.OData.Client;
using Medallion.OData.Trees;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Service.Sql
{
    /// <summary>
    /// Implements <see cref="SqlSyntax"/> for MS SQLSERVER
    /// </summary>
    public class SqlServerSyntax : SqlSyntax
    {
        /// <summary>
        /// Represents SqlServer versions that altered syntax in a way that affects SQL generation
        /// </summary>
        public enum Version
        {
            /// <summary>
            /// Use for versions 2008 and below
            /// </summary>
            Sql2008,
            /// <summary>
            /// Use for versions 2012 and above
            /// </summary>
            Sql2012,
        }

        private readonly Version version;

        /// <summary>
        /// Constructs a provider for the given SqlServer <see cref="Version"/>
        /// </summary>
        public SqlServerSyntax(Version version = Version.Sql2008)
        {
            this.version = version;
        }

        /// <summary>
        /// Determines the <see cref="Version"/> based on the given <paramref name="connection"/>
        /// </summary>
        public static Version GetVersion(SqlConnection connection)
        {
            Throw.IfNull(connection, "connection");

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }
            using (var command = connection.CreateCommand())
            {
                // from http://stackoverflow.com/questions/59444/how-do-you-check-what-version-of-sql-server-for-a-database-using-tsql
                command.CommandText = @"SELECT SERVERPROPERTY('productversion')";
                var result = (string)command.ExecuteScalar();
                var majorVersion = int.Parse(result.Split('.')[0]);
                return majorVersion >= 11 ? Version.Sql2012 : Version.Sql2008;
            }
        }

        /// <summary>
        /// Maps <see cref="ODataExpressionType"/>s to SqlServer types
        /// </summary>
        protected internal override string GetSqlTypeName(ODataExpressionType oDataType)
        {
            switch (oDataType) 
            {
                case ODataExpressionType.Binary:
                    return "VARBINARY(MAX)";
                case ODataExpressionType.Boolean:
                    return "BIT";
                case ODataExpressionType.Byte:
                    return "TINYINT";
                case ODataExpressionType.DateTime:
                    return "DATETIME";
                case ODataExpressionType.DateTimeOffset:
                    return "DATETIMEOFFSET";
                case ODataExpressionType.Decimal:
                    // basing this off NHibernate mapping referenced here 
                    // http://stackoverflow.com/questions/3152439/what-is-a-good-mapping-of-net-decimal-to-sql-server-decimal
                    return "DECIMAL(19, 5)";
                case ODataExpressionType.Double:
                    return "FLOAT";
                case ODataExpressionType.Guid:
                    return "UNIQUEIDENTIFIER";
                case ODataExpressionType.Int16:
                    return "SMALLINT";
                case ODataExpressionType.Int32:
                    return "INT";
                case ODataExpressionType.Int64:
                    return "BIGINT";
                case ODataExpressionType.Single:
                    // based on http://stackoverflow.com/questions/546150/float-in-database-to-in-net
                    return "REAL";
                case ODataExpressionType.String:
                    return "NVARCHAR(MAX)";
                case ODataExpressionType.Time:
                    return "TIME";
                default:
                    throw new NotSupportedException("OData type " + oDataType + " cannot be translated to SQL");
            }
        }

        /// <summary>
        /// Returns false: SqlServer has LTRIM and RTRIM
        /// </summary>
        protected internal override bool HasTwoSidedTrim { get { return false; } }

        /// <summary>
        /// Uses [] escaping
        /// </summary>
        protected internal override void RenderColumnName(Action<string> writer, PropertyInfo member)
        {
            var escaped = string.Format("[{0}]", member.Name.Replace("]", "]]"));
            writer(escaped);
        }

        /// <summary>
        /// Uses the DATEPART function
        /// </summary>
        protected internal override void RenderDatePartFunctionCall(ODataFunction datePartFunction, Action<string> writer, Action renderArgument)
        {
            writer("DATEPART(");
            writer(this.GetDatePartString(datePartFunction));
            writer(", ");
            renderArgument();
            writer(")");
        }

        /// <summary>
        /// Uses the CHARINDEX function
        /// </summary>
        protected internal override void RenderIndexOfFunctionCall(Action<string> writer, Action renderNeedleArgument, Action renderHaystackArgument)
        {
            writer("(CHARINDEX(");
            renderNeedleArgument();
            writer(", ");
            renderHaystackArgument();
            writer(") -1)");
        }

        /// <summary>
        /// Uses <see cref="SqlSyntax.PaginationSyntax.OffsetFetch"/> for <see cref="Version.Sql2012"/> and <see cref="SqlSyntax.PaginationSyntax.RowNumber"/>
        /// for <see cref="Version.Sql2008"/>
        /// </summary>
        protected internal override SqlSyntax.PaginationSyntax Pagination
        {
            get { return this.version == Version.Sql2012 ? PaginationSyntax.OffsetFetch : PaginationSyntax.RowNumber; }
        }

        /// <summary>
        /// Uses the '%' operator
        /// </summary>
        protected internal override void RenderModuloOperator(Action<string> writer, Action renderLeftOperand, Action renderRightOperand)
        {
            writer("(");
            renderLeftOperand();
            writer(" % ");
            renderRightOperand();
            writer(")");
        }

        /// <summary>
        /// Renders @<see cref="Parameter.Name"/>
        /// </summary>
        protected internal override void RenderParameterReference(Action<string> writer, Parameter parameter)
        {
            writer("@");
            writer(parameter.Name);
        }

        /// <summary>
        /// Uses the ROUND function
        /// </summary>
        protected internal override void RenderRoundFunctionCall(Action<string> writer, Action renderArgument)
        {
            writer("ROUND(");
            renderArgument();
            writer(", 0)");
        }

        /// <summary>
        /// Uses the SUBSTRING function
        /// </summary>
        protected internal override void RenderSubstringFunctionCall(Action<string> writer, Action renderOriginalStringArgument, Action renderStartingPositionArgument, Action renderLengthArgument)
        {
            writer("SUBSTRING(");
            renderOriginalStringArgument();
            writer(", ");
            renderStartingPositionArgument();
            writer(" + 1");
            if (renderLengthArgument != null)
            {
                writer(", ");
                renderLengthArgument();
            }
            writer(")");
        }

        /// <summary>
        /// Uses the LEN function
        /// </summary>
        protected internal override string StringLengthFunctionName { get { return "LEN"; } }
    }
}
