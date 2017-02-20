using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Tests
{
    [TestClass]
    public class QueryStringParserTest
    {
        [TestMethod]
        public void TestArgumentValidation()
        {
            UnitTestHelpers.AssertThrows<ArgumentNullException>(() => QueryStringParser.ParseQueryString(null));
            UnitTestHelpers.AssertThrows<ArgumentNullException>(() => HttpUtility.ParseQueryString(null));
        }

        [TestMethod]
        public void TestAddAfterParse()
        {
            var p1 = QueryStringParser.ParseQueryString("a=2&c=3");
            p1["a"] = "hi";
            p1.Add("c", "x");
            p1["b"] = "bye";
            p1.ToString().ShouldEqual("a=hi&c=3&c=x&b=bye");

            var p2 = HttpUtility.ParseQueryString("a=2&c=3");
            p2["a"] = "hi";
            p2.Add("c", "x");
            p2["b"] = "bye";
            p2.ToString().ShouldEqual("a=hi&c=3&c=x&b=bye");
        }

        [TestMethod]
        public void TestAgainstHttpUtility()
        {
            Assert.IsTrue(typeof(HttpUtility).IsPublic, "don't bother running this against the shim");

            Test(string.Empty);
            Test(" ");
            Test("===");
            Test("=");
            Test("?");
            Test("??");
            Test("?a=2&b=++");
            Test("+=+");
            Test("&");
            Test("&&&&");
            Test("%23=fads&b=%2d");
            Test("a=2&a=3&b=3&b=&b");
            Test("just a big value");
            Test("a&");
            Test("a&&b&&");
        }

        private static void Test(string query)
        {
            var parse1 = QueryStringParser.ParseQueryString(query);
            var parse2 = HttpUtility.ParseQueryString(query);
            
            // ignorecase because HttpUtility.UrlEncode uses lowercase hex digits while WebUtility uses uppercase
            if (!StringComparer.OrdinalIgnoreCase.Equals(parse1.ToString(), parse2.ToString()))
            {
                Assert.Fail($"Expected '{parse2}', got '{parse1}'. Failed on '{query}'");
            }
            parse1.Count.ShouldEqual(parse2.Count, $"Failed on '{query}'");
        }
    }
}
