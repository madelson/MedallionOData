using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Medallion.OData.Tests.Client
{
    public class ODataRowTest
    {
        [Test]
        public void TestGetProperty()
        {
            Expression<Func<ODataEntity, object>> getString = r => r.Get<string>("Text");
            this.TryConvertMethodCallToRowProperty((MethodCallExpression)getString.Body, out var prop).ShouldEqual(true);
            prop.Name.ShouldEqual("Text");
            prop.PropertyType.ShouldEqual(typeof(string));

            Expression<Func<ODataEntity, object>> toString = r => r.ToString();
            this.TryConvertMethodCallToRowProperty((MethodCallExpression)toString.Body, out prop).ShouldEqual(false);
            prop.ShouldEqual(null);
        }

        [Test]
        public void TestGet()
        {
            var row = new ODataEntity(new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });
            row.Get<int>("A").ShouldEqual(1);
            row.Get<string>("b").ShouldEqual("2");
            UnitTestHelpers.AssertThrows<InvalidCastException>(() => row.Get<string>("a"));
            UnitTestHelpers.AssertThrows<ArgumentException>(() => row.Get<string>("c"));
        }

        [Test]
        public void TestSerialization()
        {
            var row = new ODataEntity(new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });
        }

        public bool TryConvertMethodCallToRowProperty(MethodCallExpression methodCall, out PropertyInfo property)
        {
            property = null;
            try
            {
                var normalized = ODataEntity.Normalize(methodCall);
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
