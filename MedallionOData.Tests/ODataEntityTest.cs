using Medallion.OData.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests
{
    [TestClass]
    public class ODataEntityTest
    {
        [TestMethod]
        public void ODataEntityTestIncompatibleGet()
        {
            var entity = new ODataEntity(new[] { KeyValuePair.Create("A", "100".As<object>()) });

            var ex = UnitTestHelpers.AssertThrows<InvalidCastException>(() => entity.Get<double?>("A"));
            ex.Message.ShouldEqual("value '100' of type System.String for property 'A' is not compatible with requested type System.Nullable`1[System.Double]");
        }

        [TestMethod]
        public void ODataEntityTestNumericCastIssue()
        {
            var entity = new ODataEntity(new[] { new KeyValuePair<string, object>("x", 1.5), new KeyValuePair<string, object>("y", long.MaxValue) });

            var ex = UnitTestHelpers.AssertThrows<InvalidCastException>(() => entity.Get<int>("x"));
            ex.Message.ShouldEqual("Failed to convert property 'x' value '1.5' of type System.Double to requested type System.Int32");

            var ex2 = UnitTestHelpers.AssertThrows<InvalidCastException>(() => entity.Get<int>("y"));
            ex2.Message.ShouldEqual("Failed to convert property 'y' value '9223372036854775807' of type System.Int64 to requested type System.Int32");
        }
    }
}
