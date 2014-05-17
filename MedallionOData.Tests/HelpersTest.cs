using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests
{
    [TestClass]
    public class HelpersTest
    {
        [TestMethod]
        public void TestGenericArguments()
        {
            this.TestGenericArguments(typeof(IQueryable<int>), typeof(IEnumerable<>), typeof(int));
            this.TestGenericArguments(typeof(IQueryable<int>), typeof(IQueryable<>), typeof(int));
        }

        private void TestGenericArguments(Type type, Type genericTypeDefinition, params Type[] expected)
        {
            var result = type.GetGenericArguments(genericTypeDefinition);
            if (!result.SequenceEqual(expected))
            {
                Assert.Fail(result.ToDelimitedString(", ") + " != " + expected.ToDelimitedString(", "));
            }
        }
    }
}
