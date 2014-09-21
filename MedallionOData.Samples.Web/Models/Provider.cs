using Medallion.OData.Service.Sql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Medallion.OData.Samples.Web.Models
{
    public class Syntax : SqlSyntax
    {
        protected override string GetSqlTypeName(Trees.ODataExpressionType oDataType)
        {
            throw new NotImplementedException();
        }
    }

    public class Executor : SqlExecutor
    {
        protected override System.Collections.IEnumerable Execute(string sql, IReadOnlyList<Parameter> parameters, Type resultType)
        {
            throw new NotImplementedException();
        }
    }
}