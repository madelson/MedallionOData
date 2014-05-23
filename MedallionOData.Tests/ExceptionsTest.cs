using Medallion.OData.Client;
using Medallion.OData.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests
{
    [TestClass]
    public class ExceptionsTest
    {
        [TestMethod]
        public void TestODataCompileException()
        {
            this.TestException<ODataCompileException>();
        }

        [TestMethod]
        public void TestODataParseException()
        {
            this.TestException<ODataParseException>();
        }

        private void TestException<TException>()
            where TException : Exception, new()
        {
            Assert.IsNotNull(Activator.CreateInstance(typeof(TException), new object[] { "message" }));
            Assert.IsNotNull(Activator.CreateInstance(typeof(TException), new object[] { "message", new Exception("inner") }));

            var ex = new TException();
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, ex);
                stream.Seek(0, SeekOrigin.Begin);
                formatter.Deserialize(stream);
            }
        }
    }
}
