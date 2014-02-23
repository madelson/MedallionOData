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
	/// A dynamic row type to provide support for dynamic client queries
	/// </summary>
	public sealed class ODataRow
	{
		private static readonly MethodInfo GetMethod = Helpers.GetMethod((ODataRow r) => r.Get<int>(null))
			.GetGenericMethodDefinition();
		private static readonly IEqualityComparer<string> KeyComparer = StringComparer.OrdinalIgnoreCase;

		private readonly IReadOnlyDictionary<string, object> _values;

		public ODataRow(IEnumerable<KeyValuePair<string, object>> values)
		{
			this._values = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, KeyComparer);
		}

		/// <summary>
		/// Gets the strongly-typed value of the named column. This method can be used in OData queries as long as <paramref name="columnName"/>
		/// is a constant or local variable capture
		/// </summary>
		/// <typeparam name="TColumn">the column type</typeparam>
		/// <param name="columnName">the column name</param>
		/// <returns>the column value</returns>
		public TColumn Get<TColumn>(string columnName)
		{
			object result;
			if (!this._values.TryGetValue(columnName, out result))
			{
				throw new ArgumentException("The row does not contain a value for column '" + columnName + "'!");
			}
			if (!typeof(TColumn).IsInstanceOfType(result))
			{
				throw new InvalidCastException(string.Format("value '{0}' for column '{1}' is not of type {2}", result ?? "null", columnName, typeof(TColumn)));
			}

			return (TColumn)result;
		}

		internal static bool TryConvertMethodCallToRowProperty(MethodCallExpression methodCall, out PropertyInfo property)
		{
			object value;
			if (methodCall.Method.MetadataToken != GetMethod.MetadataToken
				|| !Equals(methodCall.Method.Module, GetMethod.Module)
				|| !LinqHelpers.TryGetValue(methodCall.Arguments.Single(), LinqHelpers.GetValueOptions.ConstantsFieldsAndProperties, out value)
				|| value == null)
			{
				property = null;
				return false;
			}

			property = RowPropertyInfo.For(name: (string)value, type: methodCall.Method.GetGenericArguments().Single());
			return true;
		}

		#region ---- Fake property implementation ----
		private TColumn FakeGetter<TColumn>()
		{
			throw new InvalidOperationException("This getter is not valid");
		}
	
		private sealed class RowPropertyInfo : PropertyInfo
		{
			private readonly string _name;
			private readonly Type _type;
			private readonly int _metadataToken;

			private RowPropertyInfo(string name, Type type, int metadataToken)
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
				return Helpers.GetMethod((ODataRow r) => r.FakeGetter<object>())
					.GetGenericMethodDefinition()
					.MakeGenericMethod(this.PropertyType);
			}

			public override ParameterInfo[] GetIndexParameters()
			{
				return new ParameterInfo[0];
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
				Throw.If(index != null && index.Length > 0, "index: Row properties cannot have indexers");

				Throw.IfNull(obj, "obj");
				var row = obj as ODataRow;
				Throw.If(row == null, "obj: must be of type Row");
				Throw<ArgumentException>.If(!row._values.ContainsKey(this.Name), () => "the given row instance does not contain column '" + this.Name + "'");

				return row._values[this.Name];
			}

			public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, System.Globalization.CultureInfo culture)
			{
				throw new InvalidOperationException("Row properties are read-only!");
			}

			public override Type DeclaringType
			{
				get { return typeof(ODataRow); }
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
				get { return typeof(ODataRow); }
			}

			public override bool Equals(object obj)
			{
				var that = obj as RowPropertyInfo;
				return that != null && KeyComparer.Equals(this.Name, that.Name) && this.PropertyType == that.PropertyType;
			}

			public override int GetHashCode()
			{
				return KeyComparer.GetHashCode(this.Name) ^ this.PropertyType.GetHashCode();
			}

			public override string ToString()
			{
				return string.Format("{0}.Get<{1}>(\"{2}\")", typeof(ODataRow), this.PropertyType, this.Name);
			}

			// TODO consider returning a static token with a new module each time instead
			public override int MetadataToken { get { return this._metadataToken; } }
			public override Module Module { get { return RowModule.Instance; } }

			private static int _lastMetadataToken = 0;
			private static readonly ConcurrentDictionary<Tuple<Type, string>, RowPropertyInfo> Cache = new ConcurrentDictionary<Tuple<Type, string>, RowPropertyInfo>(
				EqualityComparers.Create<Tuple<Type, string>>(
					equals: (t1, t2) => t1.Item1 == t2.Item1 && KeyComparer.Equals(t1.Item2, t2.Item2),
					hash: t => t.Item1.GetHashCode() ^ KeyComparer.GetHashCode(t.Item2)
				)	
			);

			public static RowPropertyInfo For(string name, Type type)
			{
				var cacheKey = Tuple.Create(type, name);
				RowPropertyInfo cached;
				if (Cache.TryGetValue(cacheKey, out cached))
				{
					return cached;
				}

				var metadataToken = Interlocked.Increment(ref _lastMetadataToken);
				var newProp = new RowPropertyInfo(name, type, metadataToken);
				// to avoid ever having equivalent props with different tokens, we only return
				// the new prop if it becomes the cached instance
				return Cache.TryAdd(cacheKey, newProp)
					? newProp
					: Cache[cacheKey];
			}
		}

		private sealed class RowModule : Module
		{
			public static readonly Module Instance = new RowModule();
		}
		#endregion
	}
}
