using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Medallion.OData.Client;
using Medallion.OData.Trees;
using NUnit.Framework;

namespace Medallion.OData.Tests.Client
{
    /// <summary>
    /// Tests that we can implement the functionality of https://github.com/madelson/MedallionOData/pull/3
    /// with existing public APIs
    /// </summary>
    public class LinqToODataTest
    {
        #region ---- Implementation ----
        private static class LinqToOData
        {
            public static NameValueCollection ToNameValueCollection(Expression expression, ODataQueryOptions options)
            {
                return HttpUtility.ParseQueryString(ToODataExpressionLanguage(expression, options));
            }

            public static string ToODataExpressionLanguage(Expression expression, ODataQueryOptions options)
            {
                return Translate(expression, options).ToString();
            }

            public static ODataQueryExpression Translate(Expression expression, ODataQueryOptions options)
            {
                IODataClientQueryPipeline pipeline = new DefaultODataClientQueryPipeline();
                return pipeline.Translate(expression, options).ODataQuery;
            }
        }
        #endregion

        [Test]
        public void TestLinqToODataTranslate()
        {
            var query = new A[0].AsQueryable()
                .Where(a => a.B > 5)
                .OrderBy(a => a.C);

            var translated = LinqToOData.Translate(query.Expression, new ODataQueryOptions());
            translated.ToString().ShouldEqual("?$filter=B+gt+5&$orderby=C&$format=json");
        }

        [Test]
        public void TestLinqToODataToODataExpressionLanguage()
        {
            var query = new A[0].AsQueryable()
                .Select(a => new { x = a.B + 2 })
                .OrderByDescending(t => t.x);

            var oData = LinqToOData.ToODataExpressionLanguage(query.Expression, new ODataQueryOptions());
            oData.ShouldEqual("?$orderby=B+add+2+desc&$format=json&$select=B");
        }

        [Test]
        public void TestLinqToODataToNameValueCollection()
        {
            var query = new A[0].AsQueryable()
                .Where(a => a.C.Length > 0)
                .OrderBy(a => -1 * a.B)
                .Select(a => new { twice = a.C + a.C, @double = 2 * a.B });

            var collection = LinqToOData.ToNameValueCollection(query.Expression, new ODataQueryOptions(inlineCount: ODataInlineCountOption.AllPages));
            collection["$filter"].ShouldEqual("length(C) gt 0");
            collection["$orderby"].ShouldEqual("-1 mul B");
            collection["$select"].ShouldEqual("C,B");
            collection["$inlineCount"].ShouldEqual("allpages");
        }

        private class A
        {
            public int B { get; set; }
            public string C { get; set; }
        }
    }
}
