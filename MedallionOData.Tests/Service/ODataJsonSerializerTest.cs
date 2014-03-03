using Medallion.OData.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service
{
    [TestClass]
    public class ODataJsonSerializerTest
    {
        [TestMethod]
        public void SimpleTest()
        {
            ODataQueryProjectorTest.Base.Counter = 0;
            var result = this.Serialize(new Settings(), a => a.Id, a => a.Name);
            this.Compare(
                result, 
                @"[{
                    'Id': 1,
                    'Name': '1'
                }]"
            );
        }

        [TestMethod]
        public void DuplicatePathsTest()
        {
            ODataQueryProjectorTest.Base.Counter = 0;
            var result = this.Serialize(new Settings(), a => a.Id, a => a.Id);
            this.Compare(
                result,
                @"[{
                    'Id': 1
                }]"
            );
        }

        [TestMethod]
        public void NestingTest()
        {
            ODataQueryProjectorTest.Base.Counter = 0;
            var result = this.Serialize(new Settings { Count = 2 }, a => a.B.C.Id, a => a.B);
            this.Compare(
                result,
                @"[{
                    'B': {
                        'Id': 2,
                        'Name': '2',
                        'C': {
                            'Id': 3
                        }
                    }
                },
                {
                    'B': {
                        'Id': 6,
                        'Name': '6',
                        'C': {
                            'Id': 7
                        }
                    }
                }]"
            );
        }

        [TestMethod]
        public void TestWithNulls()
        {
            ODataQueryProjectorTest.Base.Counter = 0;
            var result = this.Serialize(new Settings { ModifyAction = a => { a.Name = null; } }, a => a.Name);
            this.Compare(
                result,
                @"[{
                    'Name': null
                }]"
            );
        }

        private class Settings
        {
            public int? Count { get; set; }
            public Action<ODataQueryProjectorTest.A> ModifyAction { get; set; }
        }

        private string Serialize(Settings settings, params Expression<Func<ODataQueryProjectorTest.A, object>>[] expressions)
        {
            var oDataExpressions = expressions.Select(exp => exp.Body as MemberExpression ?? ((UnaryExpression)exp.Body).Operand)
                .Select(exp => ODataQueryProjectorTest.ToODataExpression((MemberExpression)exp))
                .ToArray();

            var enumerable = Enumerable.Range(0, settings.Count ?? 1).Select(_ => new ODataQueryProjectorTest.A())
                .ToList();
            if (settings.ModifyAction != null)
            {
                enumerable.ForEach(settings.ModifyAction);
            }

            var projectionResult = ODataQueryProjector.Project(enumerable.AsQueryable(), oDataExpressions);

            var serialized = new ODataJsonSerializer().Serialize(projectionResult.Query, projectionResult.Mapping, inlineCount: null);
            return serialized;
        }

        private void Compare(string json1, string json2)
        {
            var standardJson1 = Standardize(json1);
            var standardJson2 = Standardize("{ 'value': " + json2 + "}");
            standardJson1.ShouldEqual(standardJson2);
        }

        private static string Standardize(string json)
        {
            return Regex.Replace(json.Replace("\"", "'"), @"\s+", string.Empty).Trim();
        }
    }
}
