using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData
{
    internal static class EqualityComparers
    {
        /// <summary>
        /// Creates a <see cref="EqualityComparer{T}"/> from the given equals and hash functions
        /// </summary>
        /// <typeparam name="T">the type to be compared</typeparam>
        /// <param name="equals">a function which returns true if two instances of the given type are equal. This function need not handle nulls</param>
        /// <param name="hash">an optional function which computes a hash code for the given type. This function need not handle nulls</param>
        /// <param name="schema">optionally specifies an example "schema" object to enable type-inference for anonymous types</param>
        public static EqualityComparer<T> Create<T>(Func<T, T, bool> equals, Func<T, int> hash = null, T schema = default(T))
        {
            Throw.IfNull(equals, "equals");

            return new FuncEqualityComparer<T>(equals, hash);
        }

        /// <summary>
        /// Creates an <see cref="EqualityComparer{T}"/> from which compares objects of type T via the keys returned by the given key selector
        /// </summary>
        /// <typeparam name="T">the type to be compared</typeparam>
        /// <typeparam name="TKey">the key type to use for comparison</typeparam>
        /// <param name="keySelector">returns a key for a given T instance by which instances can be compared. This function need not handle nulls</param>
        /// <param name="comparer">an optional comparer specifying how keys are compared</param>
        /// <param name="schema">optionally specifies an example "schema" object to enable type-inference for anonymous types</param>
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
