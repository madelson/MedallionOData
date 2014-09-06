using Medallion.OData.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Dynamic
{
    /// <summary>
    /// Represents an object value in OData
    /// </summary>
    [JsonConverter(typeof(ODataObject.JsonConverter))]
    public abstract class ODataObject
    {
        // prevent externa inheritors
        internal ODataObject()
        {
        }

        #region ---- Serialization ----
        private sealed class JsonConverter : Newtonsoft.Json.JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(ODataObject).IsAssignableFrom(objectType);
            }

            #region ---- Read ----
            public override bool CanRead { get { return true; } }

            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            {
                var jToken = serializer.Deserialize<JToken>(reader);
                return ConvertJToken(jToken, keepPrimitiveValues: false);
            }

            private static object ConvertJToken(JToken token, bool keepPrimitiveValues)
            {
                if (token == null)
                {
                    return null;
                }
                switch (token.Type)
                {
                    case JTokenType.Object:
                        var keyValuePairs = ((JObject)token).As<IDictionary<string, JToken>>()
                            .Select(kvp => KeyValuePair.Create(kvp.Key, ConvertJToken(kvp.Value, keepPrimitiveValues: true)));
                        return new ODataEntity(keyValuePairs);
                    case JTokenType.Array:
                        return ((JArray)token).Select(o => ConvertJToken(o, keepPrimitiveValues: true)).ToList();
                    default:
                        var value = token as JValue;
                        if (value == null)
                        {
                            throw new NotSupportedException("Cannot convert JSON token of type " + token.Type);
                        }
                        return keepPrimitiveValues ? value.Value : ODataValue.FromObject(value.Value);
                }
            }
            #endregion

            #region ---- Write ----
            public override bool CanWrite { get { return true; } }

            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            {
                var oDataValue = value as ODataValue;
                if (oDataValue != null)
                {
                    // for ODataValue, we just serialize the inner value
                    serializer.Serialize(writer, oDataValue.Value);
                }
                else
                {
                    // ODataEntity just serializes as a dictionary
                    serializer.Serialize(writer, ((ODataEntity)value).Values);
                }
            }
            #endregion
        }
        #endregion
    }
}
