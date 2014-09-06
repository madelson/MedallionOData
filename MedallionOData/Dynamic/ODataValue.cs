using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;

namespace Medallion.OData.Dynamic
{
    /// <summary>
    /// Represents a primitive value in OData
    /// </summary>
    public sealed class ODataValue : ODataObject, IComparable<ODataValue>, IComparable
    {
        internal ODataValue(object value)
        {
            this.value = value;
        }

        private readonly object value;
        /// <summary>
        /// The underlying C# value for this <see cref="ODataValue"/>. Cannot be null
        /// </summary>
        public object Value { get { return this.value; } }

        /// <summary>
        /// Returns a <see cref="ODataValue"/> for the given <paramref name="value"/> if possible
        /// </summary>
        public static ODataValue FromObject(object value)
        {
            if (value == null)
            {
                return null;
            }

            switch (value.GetType().ToODataExpressionType()) 
            {
                case ODataExpressionType.Unknown:
                    var oDataValue = value as ODataValue;
                    if (oDataValue == null)
                    {
                        throw new ArgumentException("value: cannot convert " + value.GetType() + " to an ODataValue");
                    }
                    return oDataValue;
                case ODataExpressionType.Complex:
                    throw new ArgumentException("value: cannot create an " + typeof(ODataValue) + " from a complex object");
                default:
                    return new ODataValue(value);
            }
        }

        #region ---- IComparable ----
        // Note: we implement IComparable so that you can do in-memory sorts based on ODataValues

        int IComparable<ODataValue>.CompareTo(ODataValue that)
        {
            if (that == null)
            {
                return 1; // see "a".CompareTo(null)
            }
            return Comparer<object>.Default.Compare(this.Value, that.Value);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1; // see "a".CompareTo(null)
            }

            var that = obj as ODataValue;
            Throw.If(that == null, "obj: must be of type ODataValue");
            return Comparer<object>.Default.Compare(this.Value, that.Value);
        }
        #endregion
    }
}
