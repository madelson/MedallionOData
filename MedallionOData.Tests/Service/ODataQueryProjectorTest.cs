using Medallion.OData.Service;
using Medallion.OData.Trees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service
{
    [TestClass]
    public class ODataQueryProjectorTest
    {
        [TestMethod]
        public void TestSelectId()
        {
            this.TestProjection(Select(a => a.Id));
        }

        [TestMethod]
        public void TestSelectNested()
        {
            this.TestProjection(Select(a => a.B.C.Name), Select(a => a.C.Id));
        }

        [TestMethod]
        public void TestSelectMany()
        {
            this.TestProjection(
                Select(a => a.Id),
                Select(a => a.Name),
                Select(a => a.B),
                Select(a => a.B.Id),
                Select(a => a.B.Name),
                Select(a => a.B.C),
                Select(a => a.B.C.Id),
                Select(a => a.B.C.Name),
                Select(a => a.C),
                Select(a => a.C.Id),
                Select(a => a.C.Name)
            );
        }

        internal static MemberExpression Select<TColumn>(Expression<Func<A, TColumn>> exp) 
        {
            return exp.Body as MemberExpression;
        }

        private void TestProjection(params MemberExpression[] selections)
        {
            // convert selections to expressions
            var selectColumns = selections.Select(ToODataExpression).ToArray();

            // get query
            var items = Enumerable.Range(0, 10).Select(_ => new A()).ToArray();
            
            // project
            var result = ODataQueryProjector.Project(items.AsQueryable(), selectColumns);
            var resultItems = result.Query.Cast<object>().ToArray();

            // validate
            var lambdas = selections.Select(exp => exp == null
                    ? a => a
                    : Expression.Lambda<Func<A, object>>(
                        Expression.Convert(exp, typeof(object)), 
                        (ParameterExpression)Traverse.Along(exp, e => e.Expression as MemberExpression).Last().Expression
                    ).Compile()
                )
                .ToArray();
            for (var i = 0; i < items.Length; ++i)
            {
                for (var j = 0; j < selectColumns.Length; ++j)
                {
                    var expected = lambdas[j](items[i]);
                    var cmp = expected.NullSafe(o => ODataRoundTripTest.GetComparer(o.GetType()), EqualityComparer<object>.Default);

                    var path = result.Mapping[selectColumns[j]];
                    var actual = path.Aggregate(seed: resultItems[i], func: (acc, prop) => prop.GetValue(acc));

                    cmp.Equals(actual, expected).ShouldEqual(true, actual + " vs. " + expected);
                }
            }
        }

        internal static ODataSelectColumnExpression ToODataExpression(MemberExpression selection)
        {
            Func<Expression, ODataMemberAccessExpression> translate = null;
            translate = exp => (exp as MemberExpression) == null ? null : ODataExpression.MemberAccess(translate(((MemberExpression)exp).Expression), (PropertyInfo)((MemberExpression)exp).Member);

            var memberAccess = translate(selection);
            return ODataExpression.SelectColumn(memberAccess, allColumns: memberAccess == null || memberAccess.Type == ODataExpressionType.Complex);
        }

        #region ---- Data Classes ----
        internal abstract class Base
        {
            public static int Counter;

            protected Base() 
            {
                this.Id = ++Counter;
                this.Name = this.Id.ToString();
            }

            public int Id { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
 	             return "{"
                     + this.GetType().GetProperties().Select(p => p.Name + ": " + p.GetValue(this)).ToDelimitedString(", ")
                     + "}";
            }
        }

        internal class A : Base
        {
            public A()
            {
                this.B = new B();
                this.C = new C();
            }

            public B B { get; set; }
            public C C { get; set; }
        }

        internal class B : Base
        {
            public B()
            {
                this.C = new C();
            }

            public C C { get; set; }
        }

        internal class C : Base 
        {
        }
        #endregion
    }
}
