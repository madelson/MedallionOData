using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Medallion.OData.Client;
using Medallion.OData.Parser;
using Medallion.OData.Service;
using Medallion.OData.Trees;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace Medallion.OData.Tests
{
	[TestClass]
	public class ODataRoundTripTest
	{
		[TestMethod]
		public void SimpleTest()
		{
			this.VerifyQuery(q => q);
		}

        [TestMethod]
		public void TestWhere()
		{
			this.VerifyQuery(q => q.Where(a => a.Int > 2 && a.NullableDouble > .1));
			this.VerifyQuery(q => q.Where(a => a.Int % 2 == 1).Where(a => a.NullableDouble > 0));
		}

        [TestMethod]
		public void TestOrderBy()
		{
			this.VerifyQuery(q => q.OrderBy(a => a.Int));
			this.VerifyQuery(q => q.OrderByDescending(a => a.Text).ThenBy(a => a.Int));
			// MA: note that double order by is meaningless in SQL (the first should get dropped), but is not technically
			// meaningless in linq to objects because the OrderBy sort is stable. Thus, here we ensure that the second order by
			// will erase all ordering done by the previous order by to test this in linq to objects
			this.VerifyQuery(q => q.OrderByDescending(a => a.Int % 2).OrderBy(a => a.Int % 2).ThenByDescending(a => a.Text));
		}

        [TestMethod]
		public void TestTake()
		{
			this.VerifyQuery(q => q.Take(0), requireNonEmpty: false);
			this.VerifyQuery(q => q.Take(1000));
			this.VerifyQuery(q => q.Take(8));
		}

        [TestMethod]
		public void TestSkip()
		{
			this.VerifyQuery(q => q.Skip(0));
			this.VerifyQuery(q => q.Skip(1000), requireNonEmpty: false);
			this.VerifyQuery(q => q.Skip(8));
		}

        [TestMethod]
		public void TestCombined()
		{
			this.VerifyQuery(q => q.Where(a => a.Int != 3).OrderBy(a => a.Int).Skip(3).Take(4));
			this.VerifyQuery(q => q.Where(a => a.Int > 3).OrderBy(a => a.Int).Take(4));
			this.VerifyQuery(q => q.Where(a => a.Int < 40).Skip(3).Take(4));
			this.VerifyQuery(q => q.OrderByDescending(a => a.Text).ThenBy(a => a.Int).Skip(3).Take(4));
		}

        [TestMethod]
		public void TestOutOfOrder()
		{
			var outOfOrders = new Func<IQueryable<A>, IQueryable<A>>[]
			{
				q => q.OrderBy(a => a.Int).Where(a => a.Int > 0),
				q => q.Take(3).Skip(1),
				q => q.Take(1).Where(a => a.Int > 0),
				q => q.Skip(2).Where(a => a.Int > 0),
				q => q.Take(1).OrderBy(a => a.Int),
				q => q.Skip(1).OrderBy(a => a.Int),
				q => q.Where(a => a.Int > 0).OrderBy(a => a.Int).Where(a => a.Int < 0)
			};
			foreach (var outOfOrder in outOfOrders)
			{
				UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.VerifyQuery(outOfOrder));
			}
		}

		[TestMethod]
		public void TestNestedQuery()
		{
			var randomQuery = this.RandomQueryable(new Random(654321));
            UnitTestHelpers.AssertThrows<ODataCompileException>(() => this.VerifyQuery(q => q.Where(a => randomQuery.Any(aa => aa.Int > a.Int))));
		}

		[TestMethod]
		public void TestStringMethods()
		{
			this.VerifyQuery(q => q.Where(a => (a.Text + "_a").EndsWith("_a") && string.Concat("a", a.Text, "b").Contains(a.Text)));
			this.VerifyQuery(q => q.Where(a => (a.Text + "a").ToUpper() == (a.Text + "A"))
				.Where(a => a.Text + "B" != (a.Text + "B").ToLower()));
			this.VerifyQuery(q => q.OrderByDescending(a => a.Text.Length));
			this.VerifyQuery(q => q.Where(a => (a.Text + "abcd").Substring(1).Length > 4));
			this.VerifyQuery(q => q.Where(a => "abcdefg".ToUpper().Substring(1, 4).Substring(1, 2).Substring(1).Length == 1));
			this.VerifyQuery(q => q.Where(a => "aaa".Replace("aa", "bb") == "bba"));
		}

		[TestMethod]
		public void TestContains()
		{
			var array = new[] { 1, 2, 3, 4 };
			var list = array.ToList();
			var iList = list.As<IList<int>>();
			var collection = list.As<ICollection<int>>();
			var readOnlyCollection = list.As<IReadOnlyCollection<int>>();
			var enumerable = array.AsEnumerable();

			this.VerifyQuery(q => q.Where(a => array.Contains(a.Int % 2)));
			this.VerifyQuery(q => q.Where(a => list.Contains(a.Int % 2)));
			this.VerifyQuery(q => q.Where(a => iList.Contains(a.Int % 2)));
			this.VerifyQuery(q => q.Where(a => collection.Contains(a.Int % 2)));
			this.VerifyQuery(q => q.Where(a => readOnlyCollection.Contains(a.Int % 2)));
			this.VerifyQuery(q => q.Where(a => enumerable.Contains(a.Int % 2)));
		}

		[TestMethod]
		public void TestRow()
		{
			this.VerifyQuery((IQueryable<ODataRow> q) => q.Where(r => r.Get<int>("Int") > 0), q => q.Where(a => a.Int > 0));
			this.VerifyQuery((IQueryable<ODataRow> q) => q.Where(r => r.Get<double?>("NullableDouble").HasValue), q => q.Where(r => r.NullableDouble.HasValue));
			this.VerifyQuery((IQueryable<ODataRow> q) => q.OrderBy(r => r.Get<double?>("NullableDouble").HasValue), q => q.OrderBy(r => r.NullableDouble.HasValue));
			this.VerifyQuery(
				(IQueryable<ODataRow> q) => q.OrderByDescending(r => r.Get<string>("Text")).ThenByDescending(r => r.Get<double?>("NullableDouble").HasValue).Skip(5).Take(100),
				q => q.OrderByDescending(r => r.Text).ThenByDescending(r => r.NullableDouble.HasValue).Skip(5).Take(100)
			);
		}

		[TestMethod]
		public void TestRowErrors()
		{
            UnitTestHelpers.AssertThrows<ArgumentException>(() => this.VerifyQuery((IQueryable<ODataRow> q) => q.Where(r => r.Get<string>("NullableDouble") != "0"), q => q));
            UnitTestHelpers.AssertThrows<ODataParseException>(() => this.VerifyQuery((IQueryable<ODataRow> q) => q.Where(r => r.Get<int>("FakeInt") < 100), q => q));
		}

		[TestMethod]
		public void TestNestedProperties()
		{
			this.VerifyQuery(q => q.Where(a => a.B.Id > 1));
		}

		[TestMethod]
		public void TestCapturing()
		{
			this.VerifyQuery(q => q.Where(a => a.Text != string.Empty));
			var capture = (int)Math.Sqrt(4);
			this.VerifyQuery(q => q.Where(a => a.Int > capture));
		}

		[TestMethod]
		public void TestAnonymousProjection()
		{
			// just projection
			this.VerifyQuery(q => q.Select(a => new { x = a.Int + 2 }));

			// post filter
			this.VerifyQuery(q => q.Select(a => new { x = a.Int + 2, y = a.Text + a.Text }).Where(t => (t.y.Length % 3) != (t.x % 3)));

			// pre sort
			this.VerifyQuery(q => q.OrderBy(a => a.Int % 5).Select(a => new { x = a.Int * a.Int, y = a.Int }));

			// multiple select
			this.VerifyQuery(q => q.Where(a => a.NullableDouble.HasValue).Select(a => new { x = a.Int - a.Text.Length, y = a.B.Id * a.Int }).Select(t => new { z = t.x + t.y }).Where(t => t.z % 2 == 1));

			// nested select
			this.VerifyQuery(q => q.Select(a => new { a, b = new { x = a.B.Id + 2 } }).OrderBy(t => t.a.Int % t.b.x));

			// select parameter
			this.VerifyQuery(q => q.Select(a => new { a }).Select(t => new { t }));
		}

		[TestMethod]
		public void TestInitializerProjection()
		{
			// just projection
			this.VerifyQuery(q => q.Select(a => new A { Int = a.Int + 2 }));

			// post filter
			this.VerifyQuery(q => q.Select(a => new A { Int = a.Int + 2, Text = a.Text + a.Text }).Where(a => (a.Text.Length % 3) != (a.Int % 3)));

			// pre sort
			this.VerifyQuery(q => q.OrderBy(a => a.Int % 5).Select(a => new A { Int = a.Int * a.Int, NullableDouble = a.Int }));

			// multiple select
			this.VerifyQuery(q => q.Where(a => a.NullableDouble.HasValue).Select(a => new A { Int = a.Int - a.Text.Length, NullableDouble = a.B.Id * a.Int }).Select(a => new A { NullableDouble = a.Int + a.NullableDouble }).Where(a => a.NullableDouble % 2 == 1));

			// nested select
			this.VerifyQuery(q => q.Select(a => new A { Int = a.Text.Length, B = new B { Id = a.B.Id + 2 } }).OrderBy(a => a.Int % a.B.Id));
		}

		[TestMethod]
		public void TestSimpleProjection()
		{
			// project constant
			this.VerifyQuery(q => q.Select(a => "a").Where(a => a.Length == 1));

			// project parameter
			this.VerifyQuery(q => q.OrderByDescending(a => a.Text).Select(a => a));

			// project complex member
			this.VerifyQuery(q => q.Select(a => a.B).Where(b => b.Id % 3 > 1));

			// project derived
			this.VerifyQuery(q => q.Select(a => a.Int * a.Int).Where(sq => sq % 2 == 0));

			// double project
			this.VerifyQuery(q => q.Select(a => a.B).Select(b => b.Id).Where(id => id % 4 == 1));
		}

		[TestMethod]
		public void TestMixedProjection()
		{
			this.VerifyQuery(
				q => q.Select(a => new { a = new { b = new { c = new A { B = new B { Id = a.Text.Length + 45 } } } } })
					.Select(t => t.a.b)
					.Where(b => b.c.B.Id % 2 == 1)
					.Select(b => b.c.B.Id)
			);
		}

		[TestMethod]
        [Ignore] // can't really do this until we're using serialization
		public void TestProjectionWithDynamicRow()
		{
			this.VerifyQuery(
				(IQueryable<ODataRow> q) => q.Select(r => new { b = r.Get<B>("B"), c = r.Get<int>("Int") * 2 }).Where(t => t.b.Id % 3 != t.c % 3),
				q => q.Select(a => new { b = a.B, c = a.Int * 2 }).Where(t => t.b.Id % 3 == t.c % 3)
			);
		}

		private void VerifyQuery<TClient, TClientResult, TResult>(Func<IQueryable<TClient>, IQueryable<TClientResult>> clientQueryTransform, Func<IQueryable<A>, IQueryable<TResult>> expectedTransform, bool requireNonEmpty = true)
		{
            var comparer = GetComparer<TResult>();

			Func<object, object> resultTranslator;
			IQueryable rootQuery;
			var translated = new LinqToODataTranslator().Translate(clientQueryTransform(Empty<TClient>.Array.AsQueryable()).Expression, out rootQuery, out resultTranslator);
			Assert.IsInstanceOfType(translated, typeof(ODataQueryExpression));
			Assert.IsInstanceOfType(rootQuery, typeof(IQueryable<TClient>));

			var random = new Random(123456);
            var wasNonEmpty = false;
			for (var i = 0; i < 10; ++i)
			{
				var randomQuery = this.RandomQueryable(random);
				var transformed = expectedTransform(randomQuery);
				var expected = transformed.ToArray();
                wasNonEmpty |= expected.Any();
				Console.WriteLine("original = " + transformed);
				Console.WriteLine("odata = " + HttpUtility.UrlDecode(translated.ToString()));
				var rawApplied = ODataQueryFilter.Apply(randomQuery, ODataQueryParser.Parse(randomQuery.ElementType, translated.ToString()));
				var applied = (IEnumerable<TResult>)resultTranslator(rawApplied);
				Console.WriteLine("applied = " + rawApplied);
                applied.ToArray().CollectionShouldEqual(expected, comparer: comparer, orderMatters: true);
				Console.WriteLine(new string('-', 80));
			}
            if (requireNonEmpty)
            {
                wasNonEmpty.ShouldEqual(true, "At least 1 run should produce non-empty results!");
            }
		}

		private void VerifyQuery<TResult>(Func<IQueryable<A>, IQueryable<TResult>> queryTransform, bool requireNonEmpty = true)
		{
			this.VerifyQuery(queryTransform, queryTransform, requireNonEmpty);
		}

		private IQueryable<A> RandomQueryable(Random random)
		{
			var result = Enumerable.Range(0, random.Next(0, 21))
				.Select(_ => new A
				{
					Int = random.Next(-10, 11),
					NullableDouble = random.Next(2) == 1 ? random.NextDouble() : default(double?),
					Text = random.Next(-10, 11).ToString(),
					B = new B
					{
						Id = random.Next(4),
					}
				})
				.ToArray();
			return result.AsQueryable();
		}

        internal static EqualityComparer<T> GetComparer<T>()
        {
            if (typeof(T).IsClass && typeof(T) != typeof(string))
            {
                var props = typeof(T).GetProperties();
                return EqualityComparers.Create<T>((t1, t2) =>
                {
                    var result = props.All(p => GetComparer(p.PropertyType).Equals(p.GetValue(t1), p.GetValue(t2)));
                    return result;
                });
            }
            return EqualityComparer<T>.Default;
        }

        internal static IEqualityComparer GetComparer(Type type)
        {
            return (IEqualityComparer)Helpers.GetMethod(() => GetComparer<object>())
                .GetGenericMethodDefinition()
                .MakeGenericMethod(type)
                .Invoke(null, Empty<object>.Array);
        }

		private class A
		{
			public int Int { get; set; }
			public double? NullableDouble { get; set; }
			public string Text { get; set; }
			public B B { get; set; }
		}

		private class B
		{
			public int Id { get; set; }
		}
	}
}
