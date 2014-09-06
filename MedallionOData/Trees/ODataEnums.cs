using Medallion.OData.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Trees
{
    /// <summary>
    /// Represents the different types of OData expressions. Compare to <see cref="ExpressionType"/> in 
    /// LINQ expressions
    /// </summary>
	public enum ODataExpressionKind
	{
        /// <summary>
        /// Represents an entire query
        /// </summary>
		Query,

		// expression components

        /// <summary>
        /// Represents a binary operation like adition
        /// </summary>
		BinaryOp,
        /// <summary>
        /// Represents a unary operation like "not"
        /// </summary>
		UnaryOp,
        /// <summary>
        /// Represents a method call
        /// </summary>
		Call,
        /// <summary>
        /// Represents a literal value
        /// </summary>
		Constant,
        /// <summary>
        /// Represents a simple or navigation property access
        /// </summary>
		MemberAccess,
        /// <summary>
        /// Represents a cast
        /// </summary>
		Convert,
        /// <summary>
        /// Represents a value to sort by along with a sort direction
        /// </summary>
		SortKey,
        /// <summary>
        /// Represents a projected "column"
        /// </summary>
		SelectColumn,
	}

    /// <summary>
    /// Represents the OData type system. See http://msdn.microsoft.com/en-us/library/ff478141.aspx
    /// </summary>
	public enum ODataExpressionType
	{
		/// <summary>Edm.Binary</summary>
        [ODataName("'Edm.Binary'")] Binary,
        /// <summary>Edm.Boolean</summary>
        [ODataName("'Edm.Boolean'")] Boolean,
        /// <summary>Edm.Byte</summary>
		[ODataName("'Edm.Byte'")] Byte,
        /// <summary>Edm.DateTime</summary>
		[ODataName("'Edm.DateTime'")] DateTime,
        /// <summary>Edm.Decimal</summary>
		[ODataName("'Edm.Decimal'")] Decimal,
        /// <summary>Edm.Double</summary>
		[ODataName("'Edm.Double'")] Double,
        /// <summary>Edm.Single</summary>
		[ODataName("'Edm.Single'")] Single,
        /// <summary>Edm.Guid</summary>
		[ODataName("'Edm.Guid'")] Guid,
        /// <summary>Edm.Int16</summary>
		[ODataName("'Edm.Int16'")] Int16,
        /// <summary>Edm.Int32</summary>
		[ODataName("'Edm.Int32'")] Int32,
        /// <summary>Edm.Int64</summary>
		[ODataName("'Edm.Int64'")] Int64,
        /// <summary>Edm.SByte</summary>
		[ODataName("'Edm.SByte'")] SByte,
        /// <summary>Edm.String</summary>
		[ODataName("'Edm.String'")] String,
        /// <summary>Edm.Time</summary>
		[ODataName("'Edm.Time'")] Time,
        /// <summary>Edm.DateTimeOffset</summary>
		[ODataName("'Edm.DateTimeOffset'")] DateTimeOffset,

		// TODO FUTURE geography/geometry types

		/// <summary>
		/// Not really a type, but used to model "null" constants
		/// </summary>
		Null,
		/// <summary>
		/// Type constants appear in cast and isof expressions
		/// </summary>
		Type,
        /// <summary>
        /// Represents any non-primitive type, such as an entity type
        /// </summary>
		Complex,
        /// <summary>
        /// Represents an undetermined type
        /// </summary>
        Unknown,
	}

    /// <summary>
    /// The binary operations available in OData
    /// </summary>
	public enum ODataBinaryOp
	{
		// MA: organized into groups by increasing precedence level

        /// <summary>or</summary>
		[ODataName("or")] Or,

        /// <summary>and</summary>
		[ODataName("and")] And,

        /// <summary>eq</summary>
		[ODataName("eq")] Equal,
        /// <summary>ne</summary>
		[ODataName("ne")] NotEqual,
        /// <summary>gt</summary>
		[ODataName("gt")] GreaterThan,
        /// <summary>ge</summary>
		[ODataName("ge")] GreaterThanOrEqual,
        /// <summary>lt</summary>
		[ODataName("lt")] LessThan,
        /// <summary>le</summary>
		[ODataName("le")] LessThanOrEqual,

        /// <summary>add</summary>
		[ODataName("add")] Add,
        /// <summary>sub</summary>
		[ODataName("sub")] Subtract,
        /// <summary>mul</summary>
		[ODataName("mul")] Multiply,
        /// <summary>div</summary>
		[ODataName("div")] Divide,
        /// <summary>mod</summary>
		[ODataName("mod")] Modulo,
	}

    /// <summary>
    /// Represents a unary operator
    /// </summary>
	public enum ODataUnaryOp
	{
        /// <summary>not</summary>
		[ODataName("not")] Not,
	}

    /// <summary>
    /// Specifies an option for the inline count result in OData
    /// </summary>
	public enum ODataInlineCountOption
	{
        /// <summary>none</summary>
		[ODataName("none")] None,
        /// <summary>allpages</summary>
        [ODataName("allpages")]
        AllPages,	
	}

	/// <summary>
	/// From http://www.odata.org/documentation/uri-conventions/#SystemQueryOptions
	/// </summary>
	public enum ODataFunction
	{
        /// <summary>substringof</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		SubstringOf,
        /// <summary>endswith</summary>
        [ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		EndsWith,
        /// <summary>startswith</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Boolean)] 
		StartsWith,
        /// <summary>length</summary>
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.Int32)]
		Length,
        /// <summary>indexof</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.Int32)]
		IndexOf,
        /// <summary>replace</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Replace,
        /// <summary>substring</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.Int32, Returns = ODataExpressionType.String)]
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.Int32, ODataExpressionType.Int32, Returns = ODataExpressionType.String)] 
		Substring,
        /// <summary>tolower</summary>
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		ToLower,
        /// <summary>toupper</summary>
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		ToUpper,
        /// <summary>trim</summary>
		[ODataFunction(ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Trim,
        /// <summary>concat</summary>
		[ODataFunction(ODataExpressionType.String, ODataExpressionType.String, Returns = ODataExpressionType.String)] 
		Concat,
        /// <summary>day</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Day,
        /// <summary>hour</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Hour,
        /// <summary>minute</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)] 
		Minute,
        /// <summary>month</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Month,
        /// <summary>second</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Second,
        /// <summary>year</summary>
		[ODataFunction(ODataExpressionType.DateTime, Returns = ODataExpressionType.Int32)]
		Year,
        /// <summary>round</summary>
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Round,
        /// <summary>floor</summary>
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Floor,
        /// <summary>ceiling</summary>
		[ODataFunction(ODataExpressionType.Double, Returns = ODataExpressionType.Double)]
		[ODataFunction(ODataExpressionType.Decimal, Returns = ODataExpressionType.Decimal)] 
		Ceiling,
        /// <summary>isof</summary>
		[ODataFunction(ODataExpressionType.Type, Returns = ODataExpressionType.Boolean)]
		[ODataFunction(null, ODataExpressionType.Type, Returns = ODataExpressionType.Boolean)]
		IsOf,
        /// <summary>cast</summary>
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
        // We use null to represent "any" here. Should we switch this to Unknown?
        // My current thinking is NO, since Unknown represents not determined rather than "object"

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
                { ODataExpressionType.Unknown,        typeof(ODataObject) },
			};

		private static readonly Dictionary<Type, ODataExpressionType> ClrToODataTypes = ODataToClrTypes
			.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

		internal static ODataExpressionType ToODataExpressionType(this Type @this)
		{
            Throw.IfNull(@this, "this");

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

            // ODataObject is abstract, so we special-case ODataValue
            if (@this == typeof(ODataValue))
            {
                return ODataExpressionType.Unknown;
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

        internal static bool IsCompatibleWith(this ODataExpressionType @this, Type clrType)
        {
            Throw.IfNull(clrType, "clrType");

            if (@this == ODataExpressionType.Null)
            {
                return clrType.CanBeNull(); // all nullable types are compatible with the null type
            }

            var mappedODataType = clrType.ToODataExpressionType();
            return mappedODataType == @this;
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

        public static bool IsNumeric(this ODataExpressionType @this)
        {
            switch (@this)
            {
                case ODataExpressionType.Byte:
                case ODataExpressionType.Decimal:
                case ODataExpressionType.Double:
                case ODataExpressionType.Int16:
                case ODataExpressionType.Int32:
                case ODataExpressionType.Int64:
                case ODataExpressionType.SByte:
                case ODataExpressionType.Single:
                    return true;
                default:
                    return false;
            }
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
