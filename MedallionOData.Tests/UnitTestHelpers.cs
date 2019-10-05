using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Medallion.OData.Tests
{
    public static class UnitTestHelpers
    {
        public static T ShouldEqual<T>(this T @this, T that, string message = null)
        {
            Assert.AreEqual(that, @this, message);
            return @this;
        }

        public static IEnumerable<T> CollectionShouldEqual<T>(this IEnumerable<T> @this, IEnumerable<T> that, string message = null, IEqualityComparer<T> comparer = null, bool orderMatters = false)
        {
            if (@this == null || that == null)
            {
                return @this.ShouldEqual(that);
            }

            var cmp = comparer ?? EqualityComparer<T>.Default;
            List<Tuple<T, int>> MakeTuples(IEnumerable<T> seq) => seq.Select((t, globalIndex) => new { t, globalIndex })
                .GroupBy(tt => tt.t, cmp)
                .SelectMany(g => g.Select((tt, groupIndex) => Tuple.Create(tt.t, orderMatters ? tt.globalIndex : groupIndex)))
                .ToList();

            var actualTuples = MakeTuples(@this);
            var expectedTuples = MakeTuples(that);

            var messageBuilder = new StringBuilder();
            if (message != null)
            {
                messageBuilder.AppendLine(message);
            }
            messageBuilder.AppendLine("The collections were not equal!");
            var tupleComparer = EqualityComparers.Create<Tuple<T, int>>((t1, t2) => cmp.Equals(t1.Item1, t2.Item1) && t1.Item2 == t2.Item2, t => cmp.GetHashCode(t.Item1) ^ t.Item2);

            var missingFromActual = expectedTuples.Except(actualTuples, tupleComparer).ToList();
            if (missingFromActual.Any())
            {
                messageBuilder.AppendLine("The following values were missing from actual:");
                missingFromActual.ForEach(t => messageBuilder.AppendLine(JsonConvert.SerializeObject(t.Item1)));
                messageBuilder.AppendLine();
            }
            var missingFromExpected = actualTuples.Except(expectedTuples, tupleComparer).ToList();
            if (missingFromExpected.Any())
            {
                messageBuilder.AppendLine("The following values were missing from expected:");
                missingFromExpected.ForEach(t => messageBuilder.AppendLine(JsonConvert.SerializeObject(t.Item1)));
                messageBuilder.AppendLine();
            }

            if (missingFromActual.Any() || missingFromExpected.Any())
            {
                messageBuilder.AppendLine("Actual:");
                actualTuples.ForEach(t => messageBuilder.AppendLine(JsonConvert.SerializeObject(t.Item1)));
                messageBuilder.AppendLine().AppendLine("Expected:");
                expectedTuples.ForEach(t => messageBuilder.AppendLine(JsonConvert.SerializeObject(t.Item1)));
                Assert.Fail(messageBuilder.ToString());
            }

            return @this;
        }

        public static TException AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                var result = ex as TException;
                if (result == null)
                {
                    Assert.Fail("Expected {0}, got {1}", typeof(TException), ex);
                }
                return result;
            }

            Assert.Fail("Expected {0}, but no exception was thrown", typeof(TException));
            return null;
        }

        public static void AssertDoesNotThrow(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail("Threw " + ex.ToString());
            }
        }
    }
}
