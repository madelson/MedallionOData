using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Medallion.OData.Dynamic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Medallion.OData.Tests.Dynamic
{
    public class ODataObjectTest
    {
        [Test]
        public void TestFromObject()
        {
            var v1 = ODataValue.FromObject(1);
            v1.Value.ShouldEqual(1);

            var v2 = ODataValue.FromObject(null);
            v2.ShouldEqual(null);

            var v3 = ODataValue.FromObject(v1);
            ReferenceEquals(v1, v3).ShouldEqual(true);

            UnitTestHelpers.AssertThrows<ArgumentException>(() => ODataValue.FromObject(new { a = 2 }));
            UnitTestHelpers.AssertThrows<ArgumentException>(() => ODataValue.FromObject(new ODataEntity(Enumerable.Empty<KeyValuePair<string, object>>())));
        }

        [Test]
        public void TestGetValuesFromODataEntity()
        {
            var entity = new ODataEntity(new Dictionary<string, object>
            {
                { "A", null },
                { "B", 1 },
                { "C", new ODataEntity(new Dictionary<string, object> { { "X", "abc" } }) },
                { "D", ODataValue.FromObject(-1) },
                { "F", 1.5 },
            });

            entity.Get<string>("A").ShouldEqual(null);
            entity.Get<int>("b").ShouldEqual(1);
            entity.Get<int?>("b").ShouldEqual(1);
            entity.Get<ODataValue>("B").Value.ShouldEqual(1);
            entity.Get<ODataEntity>("c").Get<string>("x").ShouldEqual("abc");
            entity.Get<ODataObject>("C").GetType().ShouldEqual(typeof(ODataEntity));
            entity.Get<int>("d").ShouldEqual(-1);

            UnitTestHelpers.AssertThrows<InvalidCastException>(() => entity.Get<int>("a"));
            UnitTestHelpers.AssertThrows<InvalidCastException>(() => entity.Get<long>("F"));
        }

        [Test]
        public void TestSerialization()
        {
            var serializedODataEntity = @"{ a: 2, b: 'abc', c: { x: 2 } }";
            var entity = JsonConvert.DeserializeObject<ODataEntity>(serializedODataEntity);
            entity.Get<ODataEntity>("C").Get<int>("X").ShouldEqual(2);
            var @object = JsonConvert.DeserializeObject<ODataObject>(JsonConvert.SerializeObject(entity));
            ((ODataEntity)@object).Get<ODataEntity>("C").Get<int>("X").ShouldEqual(2);

            JsonConvert.DeserializeObject<ODataObject>("null").ShouldEqual(null);

            JsonConvert.DeserializeObject<ODataValue>("1").Value.ShouldEqual(1L);
            JsonConvert.SerializeObject(ODataValue.FromObject("abc")).ShouldEqual("\"abc\"");
        }
    }
}
