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

namespace Medallion.OData.Client
{
	/// <summary>
	/// A dynamic entity type to provide support for dynamic client queries
	/// </summary>
	[JsonConverter(typeof(ODataEntity.JsonConverter))]
    public sealed class ODataEntity
	{
		private static readonly IEqualityComparer<string> KeyComparer = StringComparer.OrdinalIgnoreCase;

		private readonly IReadOnlyDictionary<string, object> _values;

        /// <summary>
        /// Constructs a entity from the given set of key value pairs
        /// </summary>
		public ODataEntity(IEnumerable<KeyValuePair<string, object>> values)
		{
			this._values = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, KeyComparer);
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
			object result;
			if (!this._values.TryGetValue(propertyName, out result))
			{
				throw new ArgumentException("The entity does not contain a value for property '" + propertyName + "'!");
			}

            if (result == null)
            {
                if (!typeof(TProperty).CanBeNull())
                {
                    throw new InvalidCastException(string.Format("Property '{0}' has a null value and cannot be cast to '{1}'", propertyName, typeof(TProperty)));
                }
                return default(TProperty);
            }

			if (result is TProperty)
			{
                return (TProperty)result;
            }

			throw new InvalidCastException(string.Format("value '{0}' for property '{1}' is not of type {2}", result ?? "null", propertyName, typeof(TProperty)));
		}

        #region ---- Translation ----
        private static readonly MethodInfo GetMethod = Helpers.GetMethod((ODataEntity r) => r.Get<int>(null))
            .GetGenericMethodDefinition();

        /// <summary>
        /// Replaces calls to <see cref="ODataEntity.Get{T}"/> with property accesses
        /// </summary>
        public static Expression Normalize(Expression expression)
        {
            var result = Normalizer.Instance.Visit(expression);
            return result;
        }

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

        /// <summary>
        /// Reverts changes to an expression made by <see cref="ODataEntity.Normalize"/>
        /// </summary>
        public static Expression Denormalize(Expression expression)
        {
            var result = Denormalizer.Instance.Visit(expression);
            return result;
        }

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

        #region ---- Fake property implementation ----
        internal static PropertyInfo GetProperty(string name, Type type)
        {
            return EntityPropertyInfo.For(name, type);
        }

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
				Throw<ArgumentException>.If(!entity._values.ContainsKey(this.Name), () => "the given entity instance does not contain property '" + this.Name + "'");

				return entity._values[this.Name];
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
		#endregion
        #endregion

        #region ---- Serialization ----
        private sealed class JsonConverter : Newtonsoft.Json.JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(ODataEntity);
            }

            #region ---- Read ----
            public override bool CanRead { get { return true; } }

            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            {
                var jObject = serializer.Deserialize<JObject>(reader);
                return ConvertJToken(jObject);
            }

            private static object ConvertJToken(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        var keyValuePairs = ((JObject)token).As<IDictionary<string, JToken>>()
                            .Select(kvp => KeyValuePair.Create(kvp.Key, ConvertJToken(kvp.Value)));
                        return new ODataEntity(keyValuePairs);
                    case JTokenType.Array:
                        return ((JArray)token).Select(ConvertJToken).ToList();
                    default:
                        var value = token as JValue;
                        if (value == null)
                        {
                            throw new NotSupportedException("Cannot convert JSON token of type " + token.Type);
                        }
                        return value.Value;
                }
            }
            #endregion

            #region ---- Write ----
            public override bool CanWrite { get { return true; } }

            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            {
                // ODataEntity just serializes as a dictionary
                serializer.Serialize(writer, ((ODataEntity)value)._values);
            }
            #endregion
        }
        #endregion
    }
}
