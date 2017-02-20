using Medallion.OData.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// TODO VNEXT: move this out of the client namespace
namespace Medallion.OData.Client
{
	/// <summary>
	/// A dynamic entity type to provide support for dynamic client queries
	/// </summary>
    public sealed class ODataEntity : ODataObject
	{
		private static readonly IEqualityComparer<string> KeyComparer = StringComparer.OrdinalIgnoreCase;

        // TODO we could potentially optimized this class by sharing "class" definitions like ExpandoObject does
        // note that we can't use ExpandoObject directly because it doesn't support case-insensitive comparison

        internal IReadOnlyDictionary<string, object> Values { get; }

        /// <summary>
        /// Constructs a entity from the given set of key value pairs
        /// </summary>
		public ODataEntity(IEnumerable<KeyValuePair<string, object>> values)
		{
            Throw.IfNull(values, "values");

            var valuesDictionary = new Dictionary<string, object>(KeyComparer);
            foreach (var kvp in values)
            {
                var oDataValue = kvp.Value as ODataValue;
                valuesDictionary.Add(kvp.Key, oDataValue != null ? oDataValue.Value : kvp.Value);
            }
            this.Values = valuesDictionary;
		}

		/// <summary>
		/// Gets the strongly-typed value of the named property. This method can be used in OData queries as long as <paramref name="propertyName"/>
		/// is a constant or local variable capture
		/// </summary>
		/// <typeparam name="TProperty">the property type</typeparam>
		/// <param name="propertyName">the property name</param>
		/// <returns>the property value</returns>
		public TProperty Get<TProperty>(string propertyName)
		{
            Throw.IfNull(propertyName, "propertyName");

			object result;
			if (!this.Values.TryGetValue(propertyName, out result))
			{
				throw new ArgumentException("The entity does not contain a value for property '" + propertyName + "'!");
			}

            if (result == null)
            {
                if (!typeof(TProperty).CanBeNull())
                {
                    throw new InvalidCastException(string.Format(
                        "Property '{0}' has a null value and cannot be cast to requested type '{1}'", 
                        propertyName, 
                        typeof(TProperty)
                    ));
                }
                return default(TProperty); // always null due to check above
            }

			if (result is TProperty)
			{
                return (TProperty)result;
            }

            var tProperty = typeof(TProperty);
            if (tProperty.IsNumeric() && result.GetType().IsNumeric())
            {
                try
                {
                    return NumberHelper.CheckedConvert<TProperty>(result);
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException(
                        string.Format(
                            "Failed to convert property '{0}' value '{1}' of type {2} to requested type {3}",
                            propertyName,
                            result,
                            result.GetType(),
                            tProperty
                        ),
                        ex
                    );
                }
            }

            if (typeof(ODataObject).IsAssignableFrom(tProperty))
            {
                // at this point, we already know it's not ODataEntity since that would be
                // handled above. Thus, we can just handle ODataValue
                return (TProperty)(object)ODataValue.FromObject(result);
            }

            throw new InvalidCastException(string.Format(
                "value '{0}' of type {1} for property '{2}' is not compatible with requested type {3}", 
                result, 
                result.GetType(), // already checked for null above!
                propertyName,
                tProperty
            ));
		}

        #region ---- Translation ----
#if NETCORE
        private static PlatformNotSupportedException ODataEntityQueriesNotSupported() =>
            new PlatformNotSupportedException($"Queries with the {typeof(ODataEntity)} class are not yet supported on .NET Core. See https://github.com/madelson/MedallionOData/issues/9 for more details");
#endif

#if !NETCORE
        private static readonly MethodInfo GetMethod = Helpers.GetMethod((ODataEntity r) => r.Get<int>(null))
            .GetGenericMethodDefinition();
#endif

        /// <summary>
        /// Replaces calls to <see cref="ODataEntity.Get{T}"/> with property accesses
        /// </summary>
        // TODO vNext this should not be public
        public static Expression Normalize(Expression expression)
        {
#if !NETCORE
            var result = Normalizer.Instance.Visit(expression);
            return result;
#else
            throw ODataEntityQueriesNotSupported();
#endif
        }

#if !NETCORE
        #region ---- Normalizer ----
        private sealed class Normalizer : ExpressionVisitor
        {
            public static readonly Normalizer Instance = new Normalizer();

            protected override Expression VisitMethodCall(MethodCallExpression methodCall)
            {
                if (methodCall.Method.MetadataToken == GetMethod.MetadataToken
                    && Equals(methodCall.Method.Module, GetMethod.Module))
                {
                    object value;
                    Throw<ODataCompileException>.If(
                        !LinqHelpers.TryGetValue(methodCall.Arguments[0], LinqHelpers.GetValueOptions.All, out value),
                        () => string.Format(
                            "Unable to extract value for parameter '{0}' of {1}. Ensure that the value for the parameter can be statically determined",
                            methodCall.Method.GetParameters()[0].Name,
                            methodCall.Method
                        )
                    );
                    var propertyName = (string)value;
                    Throw<ODataCompileException>.If(
                        string.IsNullOrWhiteSpace(propertyName),
                        () => string.Format(
                            "'{0}' value for {1} must not be null or whitespace",
                            methodCall.Method.GetParameters()[0].Name,
                            methodCall.Method
                        )
                    );

                    var property = EntityPropertyInfo.For(name: propertyName, type: methodCall.Method.GetGenericArguments()[0]);
                    return Expression.Property(this.Visit(methodCall.Object), property);
                }

                return base.VisitMethodCall(methodCall);
            }
        }
        #endregion
#endif

        /// <summary>
        /// Reverts changes to an expression made by <see cref="ODataEntity.Normalize"/>
        /// </summary>
        // TODO vNext this should not be public
        public static Expression Denormalize(Expression expression)
        {
#if !NETCORE
            var result = Denormalizer.Instance.Visit(expression);
            return result;
#else
            throw ODataEntityQueriesNotSupported();
#endif
        }

#if !NETCORE
        #region ---- Denormalizer ----
        private sealed class Denormalizer : ExpressionVisitor
        {
            public static Denormalizer Instance = new Denormalizer();

            protected override Expression VisitMember(MemberExpression node)
            {
                var entityProp = node.Member as EntityPropertyInfo;
                if (entityProp != null)
                {
                    return Expression.Call(
                        this.Visit(node.Expression),
                        GetMethod.MakeGenericMethod(entityProp.PropertyType),
                        Expression.Constant(entityProp.Name)
                    );
                }
                return base.VisitMember(node);
            }
        }
        #endregion
#endif

        #region ---- Fake property implementation ----
        internal static PropertyInfo GetProperty(string name, Type type)
        {
#if !NETCORE
            return EntityPropertyInfo.For(name, type);
#else
            throw ODataEntityQueriesNotSupported();
#endif
        }

#if !NETCORE
        private TProperty FakeGetter<TProperty>()
		{
			throw new InvalidOperationException("This getter is not valid");
		}
	
		private sealed class EntityPropertyInfo : PropertyInfo
		{
			private readonly string _name;
			private readonly Type _type;
			private readonly int _metadataToken;

			private EntityPropertyInfo(string name, Type type, int metadataToken)
			{
				Throw.IfNull(name, "name");
				Throw.IfNull(type, "type");

				this._name = name;
				this._type = type;
				this._metadataToken = metadataToken;
			}

			public override PropertyAttributes Attributes
			{
				get { return PropertyAttributes.None; }
			}

			public override bool CanRead
			{
				get { return true; }
			}

			public override bool CanWrite
			{
				get { return true; }
			}

			public override MethodInfo[] GetAccessors(bool nonPublic)
			{
				return new[] { this.GetMethod };
			}

			public override MethodInfo GetGetMethod(bool nonPublic)
			{
				// we need to implement this because it's checked by Expression.Property calls
				return Helpers.GetMethod((ODataEntity r) => r.FakeGetter<object>())
					.GetGenericMethodDefinition()
					.MakeGenericMethod(this.PropertyType);
			}

			public override ParameterInfo[] GetIndexParameters()
			{
				return Empty<ParameterInfo>.Array;
			}

			public override MethodInfo GetSetMethod(bool nonPublic)
			{
				return null; // read-only
			}

			public override Type PropertyType
			{
				get { return this._type; }
			}

			public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, System.Globalization.CultureInfo culture)
			{
				Throw.If(index != null && index.Length > 0, "index: properties cannot have indexers");

				Throw.IfNull(obj, "obj");
				var entity = obj as ODataEntity;
				Throw.If(entity == null, "obj: must be of type ODataEntity");
				Throw<ArgumentException>.If(!entity.Values.ContainsKey(this.Name), () => "the given entity instance does not contain property '" + this.Name + "'");

				return entity.Values[this.Name];
			}

			public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, System.Globalization.CultureInfo culture)
			{
				throw new InvalidOperationException("ODataEntity properties are read-only!");
			}

			public override Type DeclaringType
			{
				get { return typeof(ODataEntity); }
			}

			public override object[] GetCustomAttributes(Type attributeType, bool inherit)
			{
				return Empty<object>.Array;
			}

			public override object[] GetCustomAttributes(bool inherit)
			{
                return Empty<object>.Array;
			}

			public override bool IsDefined(Type attributeType, bool inherit)
			{
				return false;
			}

			public override string Name
			{
				get { return this._name; }
			}

			public override Type ReflectedType
			{
				get { return typeof(ODataEntity); }
			}

			public override bool Equals(object obj)
			{
				var that = obj as EntityPropertyInfo;
				return that != null && KeyComparer.Equals(this.Name, that.Name) && this.PropertyType == that.PropertyType;
			}

			public override int GetHashCode()
			{
				return KeyComparer.GetHashCode(this.Name) ^ this.PropertyType.GetHashCode();
			}

			public override string ToString()
			{
				return string.Format("{0}.Get<{1}>(\"{2}\")", typeof(ODataEntity), this.PropertyType, this.Name);
			}

			// TODO FUTURE consider returning a static token with a new module each time instead
			public override int MetadataToken { get { return this._metadataToken; } }
			public override Module Module { get { return EntityModule.Instance; } }

			private static int _lastMetadataToken = 0;
			private static readonly ConcurrentDictionary<Tuple<Type, string>, EntityPropertyInfo> Cache = new ConcurrentDictionary<Tuple<Type, string>, EntityPropertyInfo>(
				EqualityComparers.Create<Tuple<Type, string>>(
					equals: (t1, t2) => t1.Item1 == t2.Item1 && KeyComparer.Equals(t1.Item2, t2.Item2),
					hash: t => t.Item1.GetHashCode() ^ KeyComparer.GetHashCode(t.Item2)
				)	
			);

			public static EntityPropertyInfo For(string name, Type type)
			{
				var cacheKey = Tuple.Create(type, name);
				EntityPropertyInfo cached;
				if (Cache.TryGetValue(cacheKey, out cached))
				{
					return cached;
				}

				var metadataToken = Interlocked.Increment(ref _lastMetadataToken);
				var newProp = new EntityPropertyInfo(name, type, metadataToken);
				// to avoid ever having equivalent props with different tokens, we only return
				// the new prop if it becomes the cached instance
				return Cache.TryAdd(cacheKey, newProp)
					? newProp
					: Cache[cacheKey];
			}
		}

		private sealed class EntityModule : Module
		{
			public static readonly Module Instance = new EntityModule();
		}
#endif
        #endregion
#endregion
    }
}
