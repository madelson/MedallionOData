using Medallion.OData.Client;
using Medallion.OData.Parser;
using Medallion.OData.Trees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Trees
{
    [TestClass]
    public class ODataExpressionRebuildVisitorTest
    {
        [TestMethod]
        public void TestDoubling()
        {
            var visitor = new DoublingVisitor();
            ((ODataConstantExpression)visitor.Double(ODataExpression.Constant(3))).Value.ShouldEqual(6);

            var complexExpression = new ODataExpressionLanguageParser(typeof(ODataEntity), "(1 add 2) mul (2 add 4)").Parse();
            var complexExpressionDoubled = visitor.Double(complexExpression);
            complexExpressionDoubled.ToODataExpressionLanguage().ShouldEqual("(2 add 4) mul (4 add 8)");
        }

        private class DoublingVisitor : ODataExpressionRebuildVisitor 
        {
            public ODataExpression Double(ODataExpression expression) 
            {
                this.Visit(expression);
                return this.PopResult();
            }

            protected override void VisitConstant(ODataConstantExpression node)
            {
                var @int = node.Value as int?;
                if (@int.HasValue) 
                {
                    this.Return(ODataExpression.Constant(2 * @int.Value));
                }
                else 
                {
                    base.VisitConstant(node);
                }
            }
        }
    }
}
