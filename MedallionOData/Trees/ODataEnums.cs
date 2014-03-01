using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
	public enum ODataExpressionKind
	{
		Query,

		// expression components
		BinaryOp,
		UnaryOp,
		Call,
		Constant,
		MemberAccess,
		Convert,
		SortKey,
		SelectColumn,
	}

	public enum ODataExpressionType
	{
		// primitives (based on http://www.odata.org/documentation/odata-v3-documentation/)
		[ODataName("'Edm.Binary'")] Binary,
		[ODataName("'Edm.Boolean'")] Boolean,
		[ODataName("'Edm.Byte'")] Byte,
		[ODataName("'Edm.DateTime'")] DateTime,
		[ODataName("'Edm.Decimal'")] Decimal,
		[ODataName("'Edm.Double'")] Double,
		[ODataName("'Edm.Single'")] Single,
		[ODataName("'Edm.Guid'")] Guid,
		[ODataName("'Edm.Int16'")] Int16,
		[ODataName("'Edm.Int32'")] Int32,
		[ODataName("'Edm.Int64'")] Int64,
		[ODataName("'Edm.SByte'")] SByte,
		[ODataName("'Edm.String'")] String,
		[ODataName("'Edm.Time'")] Time,
		[ODataName("'Edm.DateTimeOffset'")] DateTimeOffset,

		// TODO geography/geometry types

		/// <summary>
		/// Not really a type, but used to model "null" constants
		/// </summary>
		Null,
		/// <summary>
		/// Type constants appear in cast and isof expressions
		/// </summary>
		Type,
		Complex,
	}

	public enum ODataBinaryOp
	{
		// MA: organized into groups by increasing precedence level

		[ODataName("or")] Or,
	
		[ODataName("and")] And,

		[ODataName("eq")] Equal,
		[ODataName("ne")] NotEqual,
		[ODataName("gt")] GreaterThan,
		[ODataName("ge")] GreaterThanOrEqual,
		[ODataName("lt")] LessThan,
		[ODataName("le")] LessThanOrEqual,

		[ODataName("add")] Add,
		[ODataName("sub")] Subtract,

		[ODataName("mul")] Multiply,
		[ODataName("div")] Divide,
		[ODataName("mod")] Modulo,
	}

	public enum ODataUnaryOp
	{
		[ODataName("not")] Not,
	}

	public enum ODataSortDirection
	{
		[ODataName("asc")] Ascending,
		[ODataName("desc")] Descending,
	}

	public enum ODataInlineCountOption
	{
		[ODataName("none")] None,
		[ODataName("allpages")] AllPages,	
	}

	/// <summary>
	/// From http://www.odata.org/documentation/uri-conventions/#SystemQueryOptions
	/// </summary>
	public enum ODataFunction
	{
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		SubstringOf,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		EndsWith,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		StartsWith,
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.Int32)]
		Length,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Int32)]
		IndexOf,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Replace,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.Int32, Returns = ODataExpressionType.String)]
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.Int32, ODataExpressionType.Int32, Returns = ODataExpressionType.String)] 
		Substring,
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		ToLower,
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		ToUpper,
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Trim,
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Concat,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Day,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Hour,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Minute,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Month,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Second,
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Year,
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Round,
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Floor,
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Ceiling,
		[ODataFunction(ODataExpressionType.Type, Returns = ODataExpressionType.Boolean)]
		[ODataFunction(null, ODataExpressionType.Type, Returns = ODataExpressionType.Boolean)]
		IsOf,
		[ODataFunction(null, ODataExpressionType.Type, Returns = null)]
		Cast,
	}

	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	internal class ODataNameAttribute : Attribute
	{
		public ODataNameAttribute(string name)
		{
			this.Name = name;
		}

		public string Name { get; private set; }
	}

	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
	internal class ODataFunctionAttribute : Attribute
	{
		private bool _returnTypeSet;
		private ODataExpressionType? _returnType;
		public ODataFunctionAttribute(params object[] signature)
		{
			this.Arguments = signature.Cast<ODataExpressionType?>().ToList().AsReadOnly();
		}

		public IReadOnlyList<ODataExpressionType?> Arguments { get; private set; }
		public object Returns
		{
			get { return this.ReturnType; }
			set
			{
				Throw<InvalidOperationException>.If(this._returnTypeSet, "return type can only be set once!");
				Throw<InvalidCastException>.If(value != null && !(value is ODataExpressionType?), () => "value: must be null or an instance of " + typeof(ODataExpressionType) + " found " + value);
				this._returnType = (ODataExpressionType?)value;
				this._returnTypeSet = true;
			}
		}

		public ODataExpressionType? ReturnType
		{
			get
			{
				Throw<InvalidOperationException>.If(!this._returnTypeSet, "No return type has been set!");
				return this._returnType;
			}
		}

		public override string ToString()
		{
			return string.Format("({0}) -> {1}", this.Arguments.ToDelimitedString(", "), this.Returns);
		}
	}

	internal static class ODataEnumHelpers
	{
		private static readonly Dictionary<ODataExpressionType, Type> ODataToClrTypes = new Dictionary<ODataExpressionType, Type>
			{
				{ ODataExpressionType.Binary, 		  typeof(byte[]) },
				{ ODataExpressionType.Boolean,		  typeof(bool) },
				{ ODataExpressionType.Byte,			  typeof(byte) },
				{ ODataExpressionType.DateTime,		  typeof(DateTime) },
				{ ODataExpressionType.Decimal,		  typeof(decimal) },
				{ ODataExpressionType.Double,		  typeof(double) },
				{ ODataExpressionType.Single,		  typeof(float) },
				{ ODataExpressionType.Guid,			  typeof(Guid) },
				{ ODataExpressionType.Int16,		  typeof(short) },
				{ ODataExpressionType.Int32,		  typeof(int) },
				{ ODataExpressionType.Int64,		  typeof(long) },
				{ ODataExpressionType.SByte,		  typeof(sbyte) },
				{ ODataExpressionType.String,		  typeof(string) },
				{ ODataExpressionType.Time,			  typeof(TimeSpan) },
				{ ODataExpressionType.DateTimeOffset, typeof(DateTimeOffset) },
				{ ODataExpressionType.Type,           typeof(Type) },
			};

		private static readonly Dictionary<Type, ODataExpressionType> ClrToODataTypes = ODataToClrTypes
			.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

		public static ODataExpressionType ToODataExpressionType(this Type @this)
		{
			ODataExpressionType result;
			if (ClrToODataTypes.TryGetValue(Nullable.GetUnderlyingType(@this) ?? @this, out result))
			{
				return result;
			}
			// Type is abstract, so we won't find an exact match. For now, we simply special-case it here
			if (typeof(Type).IsAssignableFrom(@this))
			{
				return ODataExpressionType.Type;
			}
			return ODataExpressionType.Complex;
		}

		public static Type ToClrType(this ODataExpressionType @this)
		{
			if (@this == ODataExpressionType.Null)
			{
				return null;
			}

			Type type;
			if (ODataToClrTypes.TryGetValue(@this, out type))
			{
				return type;
			}

			throw new ArgumentException("Expression type " + @this + " does not map to a Clr type");
		}

		public static bool IsImplicityCastableTo(this ODataExpressionType @this, ODataExpressionType that)
		{
            // can't cast to null, since it's not really a type
            var result = that != ODataExpressionType.Null
                && (
                    // null can be implicitly cast to any type
                    @this == ODataExpressionType.Null
                    || @this.ToClrType().IsImplicitlyCastableTo(that.ToClrType())
                );
			return result;
		}

		public static bool IsBooleanOp(this ODataBinaryOp @this)
		{
			return @this >= ODataBinaryOp.Or && @this <= ODataBinaryOp.LessThanOrEqual;
		}

		public static string ToODataString<TEnum>(this TEnum @this)
			where TEnum : struct, IConvertible
		{
			Throw.If(!typeof(TEnum).IsEnum, "TEnum: must be an enum type");

			var field = typeof(TEnum).GetField(@this.ToString(), BindingFlags.Public | BindingFlags.Static);
			Throw.If(field == null, "@this: was not a valid enum value");
			
			return field.GetCustomAttribute<ODataNameAttribute>().Name;
		}

		public static bool IsPrimitive(this ODataExpressionType @this)
		{
			return @this >= ODataExpressionType.Binary && @this <= ODataExpressionType.DateTimeOffset;
		}

		public static string ToODataString(this ODataFunction @this)
		{
			return @this.ToString().ToLowerInvariant();
		}
	}
}
