using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

namespace Medallion.OData.Tests.Client
{
	[TestClass]
    public class ODataRowTest
    {
		[TestMethod]
		public void TestGetProperty()
		{
			Expression<Func<ODataRow, object>> getString = r => r.Get<string>("Text");
			PropertyInfo prop;
			TryConvertMethodCallToRowProperty((MethodCallExpression)getString.Body, out prop).ShouldEqual(true);
			prop.Name.ShouldEqual("Text");
			prop.PropertyType.ShouldEqual(typeof(string));

			Expression<Func<ODataRow, object>> toString = r => r.ToString();
			TryConvertMethodCallToRowProperty((MethodCallExpression)toString.Body, out prop).ShouldEqual(false);
			prop.ShouldEqual(null);
		}

		[TestMethod]
		public void TestGet()
		{
			var row = new ODataRow(new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });
			row.Get<int>("A").ShouldEqual(1);
			row.Get<string>("b").ShouldEqual("2");
			UnitTestHelpers.AssertThrows<InvalidCastException>(() => row.Get<string>("a"));
            UnitTestHelpers.AssertThrows<ArgumentException>(() => row.Get<string>("c"));
		}

		[TestMethod]
		public void TestSerialization()
		{
			var row = new ODataRow(new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });
		}

        public bool TryConvertMethodCallToRowProperty(MethodCallExpression methodCall, out PropertyInfo property)
        {
            property = null;
            try
            {
                var normalized = ODataRow.Normalize(methodCall);
                if (normalized.NodeType == ExpressionType.MemberAccess)
                {
                    property = ((MemberExpression)normalized).Member as PropertyInfo;
                }
            }
            catch (ODataCompileException)
            {
            }

            return property != null;
        }
    }
}
