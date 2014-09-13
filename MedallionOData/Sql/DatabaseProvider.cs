using Medallion.OData.Trees;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Sql
{
    /// <summary>
    /// Presents a simple interface for accessing a database. This is an abstract class rather than an interface
    /// to allow us to add more virtual methods in the future without breaking backwards compatibility
    /// </summary>
    public abstract class DatabaseProvider
    {
        /// <summary>
        /// Gets the database name for the given <see cref="ODataExpressionType"/>. Throws <see cref="NotSupportedException"/>
        /// if no database type maps to that type
        /// </summary>
        protected internal abstract string GetSqlTypeName(ODataExpressionType oDataType);

        /// <summary>
        /// If specified CEIL is used instead of CEILING for <see cref="ODataFunction.Ceiling"/>. Defaults to FALSE (CEILING)
        /// </summary>
        protected internal virtual bool UseAbbreviatedCeilingFunction { get { return false; } }

        /// <summary>
        /// Helper function to render a "datepart" function. Defaults to the ANSI EXTRACT([date part] FROM [expression]).
        /// See http://users.atw.hu/sqlnut/sqlnut2-chp-4-sect-4.html for more information
        /// </summary>
        /// <param name="datePartFunction">An <see cref="ODataFunction"/> such as <see cref="ODataFunction.Day"/></param>
        /// <param name="writer">A function that can be called to write SQL output</param>
        /// <param name="renderArgument">A function that can be called to render the expression being operated on</param>
        protected internal virtual void RenderDatePartFunctionCall(ODataFunction datePartFunction, Action<string> writer, Action renderArgument)
        {
            writer("EXTRACT(");
            writer(this.GetDatePartString(datePartFunction));
            writer(" FROM ");
            renderArgument();
            writer(")");
        }

        internal string GetDatePartString(ODataFunction datePartFunction)
        {
            switch (datePartFunction)
            {
                case ODataFunction.Second:
                    return "SECOND";
                case ODataFunction.Minute:
                    return "MINUTE";
                case ODataFunction.Hour:
                    return "HOUR";
                case ODataFunction.Day:
                    return "DAY";
                case ODataFunction.Month:
                    return "MONTH";
                case ODataFunction.Year:
                    return "YEAR";
                default:
                    throw new ArgumentException("Function " + datePartFunction + " is not a date part function");
            }
        }

        /// <summary>
        /// Renders a call to the indexOf function. The result must used 0-based indices. The default returns (POSITION(needle IN haystack) - 1).
        /// See http://users.atw.hu/sqlnut/sqlnut2-chp-4-sect-4.html
        /// </summary>
        protected internal virtual void RenderIndexOfFunctionCall(Action<string> writer, Action renderNeedleArgument, Action renderHaystackArgument)
        {
            writer("(POSITION(");
            renderNeedleArgument();
            writer(" IN ");
            renderHaystackArgument();
            writer(") - 1)");
        }

        /// <summary>
        /// Returns the function name for <see cref="ODataFunction.Length"/>. The default returns "CHAR_LENGTH"
        /// </summary>
        protected internal virtual string StringLengthFunctionName { get { return "CHAR_LENGTH"; } }

        /// <summary>
        /// Renders a call to round(argument). The default behavior uses CASE WHEN argument > 0 THEN FLOOR(value + .5) ELSE CEILING(value - .5) END
        /// </summary>
        /// <param name="writer">writes arbitrary values</param>
        /// <param name="renderArgument">writes the argument value</param>
        protected internal virtual void RenderRoundFunctionCall(Action<string> writer, Action renderArgument)
        {
            writer("CASE WHEN ");
            renderArgument();
            writer(" > 0 THEN FLOOR(");
            renderArgument();
            writer(" + .5) ELSE ");
            writer(this.UseAbbreviatedCeilingFunction ? "CEIL" : "CEILING");
            writer("(");
            renderArgument();
            writer(" - .5) END");
        }

        /// <summary>
        /// Renders a call to substring(a, start: b [, length: c]). The default uses SUBSTRING(a FROM b + 1 [FOR c])
        /// </summary>
        /// <param name="writer">writes arbitrary sql</param>
        /// <param name="renderOriginalStringArgument">writes the first argument</param>
        /// <param name="renderStartingPositionArgument">writes the second argument</param>
        /// <param name="renderLengthArgument">if non-null, writes the third argument</param>
        protected internal virtual void RenderSubstringFunctionCall(Action<string> writer, Action renderOriginalStringArgument, Action renderStartingPositionArgument, Action renderLengthArgument)
        {
            writer("SUBSTRING(");
            renderOriginalStringArgument();
            writer(" FOR ");
            renderStartingPositionArgument();
            writer(" + 1");
            if (renderLengthArgument != null)
            {
                writer(" FOR ");
                renderLengthArgument();
            }
            writer(")");
        }

        /// <summary>
        /// Does the database provide TRIM? Otherwise, it is assumed to provide LTRIM and RTRIM (see http://users.atw.hu/sqlnut/sqlnut2-chp-4-sect-4.html).
        /// The default returns true
        /// </summary>
        protected internal virtual bool HasTwoSidedTrim { get { return true; } }

        /// <summary>
        /// Renders a modulo operation. The default is to use the ANSII standard MOD function (see http://users.atw.hu/sqlnut/sqlnut2-chp-4-sect-4.html)
        /// </summary>
        /// <param name="writer">writes arbitrary text</param>
        /// <param name="renderLeftOperand">writes the left operand (A in A % B)</param>
        /// <param name="renderRightOperand">writes the right operand (B in A % B)</param>
        protected internal virtual void RenderModuloOperator(Action<string> writer, Action renderLeftOperand, Action renderRightOperand)
        {
            writer("MOD(");
            renderLeftOperand();
            writer(", ");
            renderRightOperand();
            writer(")");
        }

        /// <summary>
        /// Renders a reference to the given <paramref name="parameter"/> in a SQL statement. By default,
        /// renders @<see cref="Parameter.Name"/>
        /// </summary>
        /// <param name="writer">writes arbitrary text</param>
        /// <param name="parameter">the parameter to reference</param>
        protected internal virtual void RenderParameterReference(Action<string> writer, Parameter parameter)
        {
            writer("@");
            writer(parameter.Name);
        }

        /// <summary>
        /// Renders the given <see cref="PropertyInfo"/> as a column name. By default, names are escaped
        /// using double quotes. See http://stackoverflow.com/questions/2901453/sql-standard-to-escape-column-names
        /// </summary>
        protected internal virtual void RenderColumnName(Action<string> writer, PropertyInfo member)
        {
            var escapedName = string.Format("\"{0}\"", member.Name.Replace("\"", "\"\""));
            writer(escapedName);
        }

        /// <summary>
        /// Specifies how pagination will be performed
        /// </summary>
        protected internal enum PaginationSyntax
        {
            /// <summary>
            /// This is the ANSII standard. See http://dba.stackexchange.com/questions/30452/ansi-iso-plans-for-limit-standardization
            /// </summary>
            OffsetFetch,
            /// <summary>
            /// Supported by SqlServer
            /// </summary>
            RowNumber,
            /// <summary>
            /// Supported by MySql. See http://stackoverflow.com/questions/3799193/mysql-data-best-way-to-implement-paging
            /// </summary>
            Limit,
        }

        /// <summary>
        /// Specifies how pagination should be performed. Defaults to <see cref="PaginationSyntax.OffsetFetch"/>
        /// </summary>
        protected internal virtual PaginationSyntax Pagination { get { return PaginationSyntax.OffsetFetch; } }

        /// <summary>
        /// Executes the given <paramref name="sql"/> query using the given <paramref name="parameters"/>. Results should be materialized
        /// to <paramref name="resultType"/>
        /// </summary>
        protected internal abstract IEnumerable Execute(string sql, IReadOnlyList<Parameter> parameters, Type resultType);

        /// <summary>
        /// Executes the given count <paramref name="sql"/> query using the given <paramref name="parameters"/>
        /// </summary>
        protected internal abstract int ExecuteCount(string sql, IReadOnlyList<Parameter> parameters);
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
