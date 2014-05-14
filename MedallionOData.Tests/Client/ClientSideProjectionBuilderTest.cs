using Medallion.OData.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Client
{
    [TestClass]
    public class ClientSideProjectionBuilderTest
    {
        [TestMethod]
        public void TestCompose()
        {
            var lambdas = new Expression<Func<T, T>>[] 
            {
                a => new T { A = a.A / 2 },
                a => new T { A = a.A + 7 },
                a => new T { A = a.A * a.A },
            };

            var result = (Expression<Func<T, T>>)ClientSideProjectionBuilder.CreateProjection(lambdas);
            var output = result.Compile()(new T { A = 6 });
            output.A.ShouldEqual(100);
        }

        [TestMethod]
        public void TestSimplify()
        {
            Expression<Func<T, T>> lambda = t => new T
            {
                A = new { x = Err<int>(1), y = new { z = Err<T>(2), val = (2 * t.A) }.val + 1 }.y,
                C = new T { A = new T { B = "abcd", C = Err<bool>(3) }.B.Length, D = new T { A = Err<int>(4) } }.A > 0,
                D = new T
                {
                    A = Convert.ToInt32(new T { A = Err<int>(5), C = true }.C),
                    B = new { m = Err<string>(6), t = new T { A = new { k = Err<bool>(7), j = 2 }.j } }.t.A.ToString(),
                }
            };

            var simplified = (Expression<Func<T, T>>)ClientSideProjectionBuilder.CreateProjection(new[] { lambda });
            var output = simplified.Compile()(new T { A = 20 });
            Assert.AreEqual(41, output.A);
            Assert.AreEqual(true, output.C);
            Assert.AreEqual(1, output.D.A);
            Assert.AreEqual("2", output.D.B);
        }

        private class T
        {
            public int A { get; set; }
            public string B { get; set; }
            public bool C { get; set; }
            public T D { get; set; }
        }

        private static T Err<T>(int i)
        {
            Assert.Fail("Failed at call " + i);
            return default(T);
        }
    }
}
