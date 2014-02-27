using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;
using Newtonsoft.Json;

using PropertyPath = System.Collections.Generic.IReadOnlyList<System.Reflection.PropertyInfo>;
using System.IO;

namespace Medallion.OData.Service
{
	internal interface IODataQuerySerializer
	{
		string Serialize(ODataQueryProjector.Result projectedQuery);
	}

	internal class ODataJsonSerializer : IODataQuerySerializer
	{
        string IODataQuerySerializer.Serialize(ODataQueryProjector.Result projectedQuery)
        {
            var node = Node.Create(projectedQuery.Mapping.Select(kvp => KeyValuePair.Create(kvp.Key, new ValueRetriever(kvp.Value))));

            using (var stringWriter = new StringWriter())
            {
                using (var writer = new JsonTextWriter(stringWriter))
                {
                    writer.WriteStartArray();
                    foreach (var item in projectedQuery.Query)
                    {
                        WriteNode(item, node, writer);
                    }
                    writer.WriteEndArray();
                }
                var result = stringWriter.ToString();
                return result;
            }
        }

        private static void WriteNode(object item, Node node, JsonWriter writer)
        {
            var oDataType = node.Property == null ? ODataExpressionType.Complex : node.Property.PropertyType.ToODataExpressionType();
            object value;
            if (oDataType == ODataExpressionType.Complex)
            {
                IEnumerable<PropertyInfo> simpleProperties;
                if (node.Select != null)
                {
                    Throw<InvalidOperationException>.If(!node.Select.AllColumns, "should have all columns!");
                    if (!node.ValueRetriever.TryGetValue(item, out value))
                    {
                        writer.WriteNull();
                        return;
                    }

                    simpleProperties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(pi => pi.CanRead && pi.PropertyType.ToODataExpressionType() != ODataExpressionType.Complex);
                }
                else
                {
                    simpleProperties = Empty<PropertyInfo>.Array;
                    value = null;
                }

                writer.WriteStartObject();
                foreach (var prop in simpleProperties)
                {
                    writer.WritePropertyName(prop.Name);
                    WriteValue(prop.GetValue(value), writer);
                }
                foreach (var child in node.Children)
                {
                    writer.WritePropertyName(child.Property.Name);
                    WriteNode(item, child, writer);
                }
                writer.WriteEndObject();
            }
            else if (node.ValueRetriever.TryGetValue(item, out value))
            {
                WriteValue(value, writer);
            }
        }

        private static void WriteValue(object value, JsonWriter writer)
        {
            // TODO consider whether we need to special-case any types (e. g. dates)
            writer.WriteValue(value);
        }

        private class Node
        {
            public PropertyInfo Property { get; private set; }
            public ODataSelectColumnExpression Select { get; private set; }
            public ValueRetriever ValueRetriever { get; private set; }
            public IEnumerable<Node> Children { get { return this._childrenByProperty.Values; } }
            private readonly Dictionary<PropertyInfo, Node> _childrenByProperty = new Dictionary<PropertyInfo, Node>(Helpers.MemberComparer);

            public static Node Create(IEnumerable<KeyValuePair<ODataSelectColumnExpression, ValueRetriever>> selects) 
            {
                var root = new Node();
                foreach (var kvp in selects) 
                {
                    Augment(root, kvp.Key, kvp.Value);
                }
                return root;
            }

            private static void Augment(Node root, ODataSelectColumnExpression select, ValueRetriever valueRetriever) 
            {
                var node = GetOrCreateMemberNode(root, select.Expression);
                if (node.Select == null) 
                {
                    node.Select = select;
                    node.ValueRetriever = valueRetriever;
                }
            }

            private static Node GetOrCreateMemberNode(Node root, ODataMemberAccessExpression memberExpression) 
            {
                if (memberExpression == null) 
                {
                    return root;
                }

                var parent = GetOrCreateMemberNode(root, memberExpression.Expression);

                Node existing;
                if (parent._childrenByProperty.TryGetValue(memberExpression.Member, out existing)) 
                {
                    return existing;
                }

                var newNode = new Node { Property = memberExpression.Member };
                parent._childrenByProperty.Add(newNode.Property, newNode);
                return newNode;
            }
        }

        private class ValueRetriever
        {
            private readonly PropertyPath _path;

            public ValueRetriever(PropertyPath path)
            {
                this._path = path;
            }

            public bool TryGetValue(object item, out object value)
            {
                var result = item;
                foreach (var prop in this._path)
                {
                    if (result == null)
                    {
                        value = null;
                        return false;
                    }
                    else
                    {
                        result = prop.GetValue(result);
                    }
                }

                value = result;
                return true;
            }
        }
    }
}
