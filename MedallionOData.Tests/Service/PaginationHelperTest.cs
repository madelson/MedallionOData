using Medallion.OData.Service;
using Medallion.OData.Trees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service
{
    [TestClass]
    public class PaginationHelperTest
    {
        [TestMethod]
        public void PaginationTestNoCountCase()
        {
            var result = new ProjectResult(count: 10, skip: 1, top: null, inlineCount: false);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(null);
            result.ExecutedCount.ShouldEqual(false);
            result.ExecutedProject.ShouldEqual(false); // leaves in queryable form
            projected.Cast<int>().Last().ShouldEqual(81);
        }

        [TestMethod]
        public void PaginationTestTopZeroOptimizationNoCount()
        {
            var result = new ProjectResult(count: 10, skip: 1, top: 0, inlineCount: false);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(null);
            result.ExecutedCount.ShouldEqual(false);
            projected.Cast<object>().Count().ShouldEqual(0);
            result.ExecutedProject.ShouldEqual(false); // even after manually executing
        }

        [TestMethod]
        public void PaginationTestTopZeroOptimizationWithCount()
        {
            var result = new ProjectResult(count: 15, skip: 3, top: 0, inlineCount: true);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(result.Count);
            result.ExecutedCount.ShouldEqual(true);
            projected.Cast<object>().Count().ShouldEqual(0);
            result.ExecutedProject.ShouldEqual(false); // even after manually executing
        }

        [TestMethod]
        public void PaginationTestFirstPageCountCase()
        {
            var result = new ProjectResult(count: 20, skip: 10, top: 10, inlineCount: true);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(result.Count);
            result.ExecutedCount.ShouldEqual(true);
            result.ExecutedProject.ShouldEqual(true);
            projected.Cast<int>().Sum().ShouldEqual(Enumerable.Range(10, count: 10).Sum(i => i * i));
        }

        [TestMethod]
        public void PaginationTestLastPageOptimization()
        {
            var result = new ProjectResult(count: 19, skip: 10, top: 10, inlineCount: true);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(result.Count);
            result.ExecutedCount.ShouldEqual(false); // optimized away
            result.ExecutedProject.ShouldEqual(true);
            projected.Cast<int>().Sum().ShouldEqual(Enumerable.Range(10, count: 9).Sum(i => i * i));
        }

        [TestMethod]
        public void PaginationTestLastPageOptimizationNoTop()
        {
            var result = new ProjectResult(count: 19, skip: 10, top: null, inlineCount: true);
            IEnumerable projected;
            int? count;
            PaginationHelper.Paginate(result, out projected, out count);
            count.ShouldEqual(result.Count);
            result.ExecutedCount.ShouldEqual(false); // optimized away
            result.ExecutedProject.ShouldEqual(true);
            projected.Cast<int>().Sum().ShouldEqual(Enumerable.Range(10, count: 9).Sum(i => i * i));
        }

        private class Record
        {
            public int Value { get; set; }
        }

        private class ProjectResult : IODataProjectResult<Record>
        {
            private readonly int skip;
            private readonly int? top;
            private readonly bool inlineCount;

            public ProjectResult(int count, int skip, int? top, bool inlineCount)
            {
                this.Count = count;
                this.skip = skip;
                this.top = top;
                this.inlineCount = inlineCount;
            }

            public int Count { get; private set; }

            public bool ExecutedProject { get; private set; }
            public bool ExecutedCount { get; private set; }

            public IQueryable ProjectedResultQuery
            {
                get 
                { 
                    return this.ResultQuery.AsEnumerable()
                        .Select(r => { this.ExecutedProject = true; return r.Value * r.Value; })
                        .AsQueryable(); 
                }
            }

            public IReadOnlyDictionary<ODataSelectColumnExpression, IReadOnlyList<PropertyInfo>> ProjectMapping
            {
                get { throw new NotImplementedException(); }
            }

            public IQueryable<Record> ResultQuery
            {
                get 
                {
                    return Enumerable.Range(0, this.Count)
                        .Skip(this.skip)
                        .Take(this.top ?? int.MaxValue)
                        .Select(i => new Record { Value = i })
                        .AsQueryable(); 
                }
            }

            public IQueryable<Record> InlineCountQuery
            {
                get 
                { 
                    return Enumerable.Range(0, this.Count)
                        .Select(i => { this.ExecutedCount = true; return new Record { Value = i }; })
                        .AsQueryable(); 
                }
            }

            public ODataQueryExpression ODataQuery
            {
                get { return ODataExpression.Query(top: this.top, skip: this.skip, inlineCount: this.inlineCount ? ODataInlineCountOption.AllPages : ODataInlineCountOption.None); }
            }
        }
    }
}
