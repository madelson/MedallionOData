using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData
{
    internal static class EqualityComparers
    {
        public static EqualityComparer<T> Create<T>(Func<T, T, bool> equals, Func<T, int> hash = null, T schema = default(T))
        {
            Throw.IfNull(equals, "equals");

            return new FuncEqualityComparer<T>(equals, hash);
        }

        public static EqualityComparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null, T schema = default(T))
        {
            Throw.IfNull(keySelector, "keySelector");

            var cmp = comparer ?? EqualityComparer<TKey>.Default;
            return Create<T>(
                equals: (a, b) => cmp.Equals(keySelector(a), keySelector(b)),
                hash: obj => cmp.GetHashCode(keySelector(obj))
            );
        }

        private class FuncEqualityComparer<T> : EqualityComparer<T>
        {
            private readonly Func<T, T, bool> equals;
            private readonly Func<T, int> hash;

            public FuncEqualityComparer(Func<T, T, bool> equals, Func<T, int> hash)
            {
                this.equals = equals;
                this.hash = hash ?? (t => 1);
            }

            public override bool Equals(T a, T b)
            {
                return a == null
                    ? b == null
                    : b != null && this.equals(a, b);
            }

            public override int GetHashCode(T obj)
            {
                return obj == null ? 0 : this.hash(obj);
            }
        }
    }
}
