using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData
{
    internal static class Helpers
    {
        public static MethodInfo GetMethod<TInstance>(Expression<Action<TInstance>> methodExpression)
        {
            Throw.IfNull(methodExpression, "methodExpression");
            var methodCall = (MethodCallExpression)methodExpression.Body;
            return methodCall.Method;
        }

        public static MethodInfo GetMethod(Expression<Action> methodExpression)
        {
            Throw.IfNull(methodExpression, "methodExpression");
            var methodCall = (MethodCallExpression)methodExpression.Body;
            return methodCall.Method;
        }

        public static PropertyInfo GetProperty<TInstance, TProperty>(Expression<Func<TInstance, TProperty>> propertyExpression)
        {
            Throw.IfNull(propertyExpression, "propertyExpression");
            var memberExpression = (MemberExpression)propertyExpression.Body;
            return (PropertyInfo)memberExpression.Member;
        }

        #region ---- Member Comparer ----
        private static readonly EqualityComparer<MemberInfo> MemberComparerField = EqualityComparers.Create<MemberInfo>(
            equals: (m1, m2) =>
            {
                if (m1.MetadataToken != m2.MetadataToken 
                    || !Equals(m1.DeclaringType, m2.DeclaringType)
                    || !Equals(m1.Module, m2.Module))
                {
                    return false;
                }
                if (m1.MemberType == MemberTypes.Method)
                {
                    var method1 = (MethodInfo)m1;
                    var method2 = (MethodInfo)m2;
                    if (method1.IsGenericMethod)
                    {
                        return method1.GetGenericArguments().SequenceEqual(method2.GetGenericArguments());
                    }
                }
                else if (m1.MemberType == MemberTypes.TypeInfo || m1.MemberType == MemberTypes.NestedType)
                {
                    var type1 = (Type)m1;
                    var type2 = (Type)m2;
                    if (type1.IsGenericType)
                    {
                        return type1.GetGenericArguments().SequenceEqual(type2.GetGenericArguments());
                    }
                }

                return true;
            },
            hash: m => m.MetadataToken
        );
        public static EqualityComparer<MemberInfo> MemberComparer { get { return MemberComparerField; } }
        #endregion

        [DebuggerStepThrough]
        public static T As<T>(this T @this) { return @this; }

        public static string ToDelimitedString<T>(this IEnumerable<T> @this, string separator = ",")
        {
            return string.Join(separator, @this);
        }

        public static Type[] GetGenericArguments(this Type @this, Type genericTypeDefinition)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(genericTypeDefinition, "genericTypeDefinition");
            Throw.If(!genericTypeDefinition.IsGenericTypeDefinition, "genericTypeDefinition: must be a generic type definition");

            if (@this.IsGenericType && @this.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                return @this.GetGenericArguments();
            }
            if (genericTypeDefinition.IsInterface)
            {
                var @interface = @this.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition);
                return @interface.NullSafe(i => i.GetGenericArguments(), Type.EmptyTypes);
            }
            return @this.BaseType.NullSafe(t => t.GetGenericArguments(genericTypeDefinition), Type.EmptyTypes);
        }

        public static bool IsGenericOfType(this Type @this, Type genericTypeDefinition)
        {
            return @this.GetGenericArguments(genericTypeDefinition).Length > 0;
        }

        public static IEnumerable<TEnum> GetValues<TEnum>()
            where TEnum : struct
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
        }

        public static IEnumerable<Tuple<TEnum, FieldInfo>> GetValuesAndFields<TEnum>()
            where TEnum : struct
        {
            var result = GetValues<TEnum>().Join(typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static), e => e.ToString(), f => f.Name, (e, f) => Tuple.Create(e, f));
            return result;
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> @this, IEqualityComparer<T> comparer = null)
        {
            Throw.IfNull(@this, "this");

            return new HashSet<T>(@this, comparer);
        }

        public static void AddRange<T>(this ICollection<T> @this, IEnumerable<T> items)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(items, "items");

            foreach (var item in items)
            {
                @this.Add(item);
            }
        }

        public static bool IsAnonymous(this Type @this)
        {
            Throw.IfNull(@this, "this");

            // HACK: The only way to detect anonymous types right now.
            return Attribute.IsDefined(@this, typeof(CompilerGeneratedAttribute), false)
                && @this.IsGenericType && @this.Name.Contains("AnonymousType")
                && (@this.Name.StartsWith("<>") || @this.Name.StartsWith("VB$"))
                && (@this.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
        }

        public static IEnumerable<T> Enumerate<T>(this T @this)
        {
            yield return @this;
        }

        public static IReadOnlyList<T> ToImmutableList<T>(this IEnumerable<T> @this)
        {
            Throw.IfNull(@this, "this");
            return new ReadOnlyCollection<T>(@this.ToArray());
        }

        public static TResult NullSafe<TInstance, TResult>(this TInstance @this, Func<TInstance, TResult> func, TResult ifNullReturn = default(TResult))
        {
            return @this != null ? func(@this) : ifNullReturn;
        }

        public static bool CanBeNull(this Type @this)
        {
            Throw.IfNull(@this, "this");
            return !@this.IsValueType || Nullable.GetUnderlyingType(@this) != null;
        }

        public static int IndexWhere<T>(this IEnumerable<T> @this, Func<T, bool> predicate)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(predicate, "predicate");

            var index = 0;
            foreach (var t in @this)
            {
                if (predicate(t))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }
    }

    internal static class LinqHelpers
    {
        [Flags]
        public enum GetValueOptions
        {
            Constants = 1,
            Fields = Constants << 1,
            Properties = Fields << 1,
            Methods = Properties << 1,
            ConstantsFieldsAndProperties = Constants | Fields | Properties,
            ConstantsFieldsPropertiesAndMethods = ConstantsFieldsAndProperties | Methods,
            All = int.MaxValue,
        }

        public static bool TryGetValue(this Expression @this, GetValueOptions options, out object value)
        {
            Throw.IfNull(@this, "this");

            switch (@this.NodeType)
            {
                case ExpressionType.Constant:
                    if (options.HasFlag(GetValueOptions.Constants))
                    {
                        value = ((ConstantExpression)@this).Value;
                        return true;
                    }
                    goto default;
                case ExpressionType.MemberAccess:
                    var doProps = options.HasFlag(GetValueOptions.Properties);
                    var doFields = options.HasFlag(GetValueOptions.Fields);
                    if (doProps || doFields)
                    {
                        var memberExpression = (MemberExpression)@this;
                        object instance = null;
                        if (memberExpression.Expression == null || memberExpression.Expression.TryGetValue(options, out instance))
                        {
                            var prop = memberExpression.Member as PropertyInfo;
                            if (doProps && prop != null)
                            {
                                value = prop.GetValue(instance);
                                return true;
                            }
                            var field = memberExpression.Member as FieldInfo;
                            if (doFields && field != null)
                            {
                                value = field.GetValue(instance);
                                return true;
                            }
                        }
                    }
                    goto default;
                case ExpressionType.Call:
                    if (options.HasFlag(GetValueOptions.Methods))
                    {
                        var methodCall = (MethodCallExpression)@this;
                        object instance = null;
                        if (methodCall.Object == null || @this.TryGetValue(options, out instance))
                        {
                            var parameters = new object[methodCall.Arguments.Count];
                            for (var i = 0; i < parameters.Length; ++i)
                            {
                                if (!methodCall.Arguments[i].TryGetValue(options, out parameters[i]))
                                {
                                    goto default;
                                }
                            }
                            value = methodCall.Method.Invoke(instance, parameters);
                            return true;
                        }
                    }
                    goto default;
                default:
                    if (options == GetValueOptions.All)
                    {
                        var lambda = Expression.Lambda<Func<object>>(Expression.Convert(@this, typeof(object)));
                        value = lambda.Compile()();
                        return true;
                    }
                    value = null;
                    return false;
            }
        }
    }

    internal static class Empty<T>
    {
        private static T[] _array;
        public static T[] Array { get { return _array ?? (_array = new T[0]); } }
    }

    internal static class Traverse
    {
        public static IEnumerable<T> Along<T>(T node, Func<T, T> next)
            where T : class
        {
            Throw.IfNull(next, "next");
            for (var current = node; current != null; current = next(current))
            {
                yield return current;
            }
        }
    }

    internal static class KeyValuePair
    {
        public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
    }

    internal static class Throw
    {
        public static void IfNull<T>(T value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void If(bool condition, string parameterName)
        {
            Throw<ArgumentException>.If(condition, parameterName);
        }

        public static void IfOutOfRange<T>(T value, string paramName, T? min = null, T? max = null)
            where T : struct, IComparable<T>
        {
            if (min.HasValue && value.CompareTo(min.Value) < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, string.Format("Expected: >= {0}, but was {1}", min, value));
            }
            if (max.HasValue && value.CompareTo(max.Value) > 0)
            {
                throw new ArgumentOutOfRangeException(paramName, string.Format("Expected: <= {0}, but was {1}", max, value));
            }
        }

        public static Exception UnexpectedCase(object value, string message = null)
        {
            throw new InvalidOperationException((message != null ? message + " " : string.Empty) + "Unexpected case value " + value);
        }
    }

    internal static class Throw<TException>
        where TException : Exception
    {
        public static void If(bool condition, string message)
        {
            if (condition)
            {
                throw (TException)Activator.CreateInstance(typeof(TException), message);
            }
        }

        public static void If(bool condition, Func<string> message)
        {
            if (condition)
            {
                throw (TException)Activator.CreateInstance(typeof(TException), message());
            }
        }
    }
}
