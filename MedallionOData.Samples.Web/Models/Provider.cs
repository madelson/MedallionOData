using Medallion.OData.Service.Sql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Medallion.OData.Samples.Web.Models
{
    public class Provider : DatabaseProvider
    {
        protected override string GetSqlTypeName(Trees.ODataExpressionType oDataType)
        {
            throw new NotImplementedException();
        }

        protected override System.Collections.IEnumerable Execute(string sql, IReadOnlyList<Parameter> parameters, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override int ExecuteCount(string sql, IReadOnlyList<Parameter> parameters)
        {
            throw new NotImplementedException();
        }
    }
}