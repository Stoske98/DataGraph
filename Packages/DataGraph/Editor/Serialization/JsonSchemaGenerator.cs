using System;
using System.Text;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Serialization
{
    /// <summary>
    /// Generates a JSON Schema file from a ParseableGraph definition.
    /// The schema can be used to validate the generated JSON data files.
    /// </summary>
    internal sealed class JsonSchemaGenerator
    {
        private int _indent;

        /// <summary>
        /// Generates a JSON Schema string for the given graph.
        /// </summary>
        public Result<string> Generate(ParseableGraph graph)
        {
            if (graph?.Root == null)
                return Result<string>.Failure("Graph or root is null.");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                _indent = 1;
                Ln(sb, "\"$schema\": \"http://json-schema.org/draft-07/schema#\",");
                Ln(sb, $"\"title\": \"{graph.GraphName}\",");
                WriteNodeSchema(sb, graph.Root, true);
                sb.AppendLine("}");
                return Result<string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Schema generation failed: {ex.Message}");
            }
        }

        private void WriteNodeSchema(StringBuilder sb, ParseableNode node, bool isLast)
        {
            switch (node)
            {
                case ParseableDictionaryRoot dict:
                    Ln(sb, "\"type\": \"object\",");
                    Ln(sb, "\"additionalProperties\": {");
                    _indent++;
                    WriteObjectProperties(sb, dict.Children);
                    _indent--;
                    Ln(sb, "}" + Trail(isLast));
                    break;

                case ParseableArrayRoot arr:
                    Ln(sb, "\"type\": \"array\",");
                    Ln(sb, "\"items\": {");
                    _indent++;
                    WriteObjectProperties(sb, arr.Children);
                    _indent--;
                    Ln(sb, "}" + Trail(isLast));
                    break;

                case ParseableObjectRoot obj:
                    WriteObjectProperties(sb, obj.Children);
                    break;
            }
        }

        private void WriteObjectProperties(StringBuilder sb, System.Collections.Generic.IReadOnlyList<ParseableNode> children)
        {
            Ln(sb, "\"type\": \"object\",");
            Ln(sb, "\"properties\": {");
            _indent++;

            for (int i = 0; i < children.Count; i++)
            {
                bool last = i == children.Count - 1;
                WriteFieldSchema(sb, children[i], last);
            }

            _indent--;
            Ln(sb, "}");
        }

        private void WriteFieldSchema(StringBuilder sb, ParseableNode node, bool isLast)
        {
            string fieldName = node.FieldName ?? "value";

            switch (node)
            {
                case ParseableCustomField custom:
                    Ln(sb, $"\"{fieldName}\": {{\"type\": \"{GetJsonType(custom.ValueType)}\"}}" + Trail(isLast));
                    break;

                case ParseableAssetField _:
                    Ln(sb, $"\"{fieldName}\": {{\"type\": \"string\"}}" + Trail(isLast));
                    break;

                case ParseableObjectField obj:
                    Ln(sb, $"\"{fieldName}\": {{");
                    _indent++;
                    WriteObjectProperties(sb, obj.Children);
                    _indent--;
                    Ln(sb, "}" + Trail(isLast));
                    break;

                case ParseableArrayField arr:
                    Ln(sb, $"\"{fieldName}\": {{");
                    _indent++;
                    Ln(sb, "\"type\": \"array\",");
                    Ln(sb, "\"items\": {");
                    _indent++;
                    WriteArrayItemSchema(sb, arr);
                    _indent--;
                    Ln(sb, "}");
                    _indent--;
                    Ln(sb, "}" + Trail(isLast));
                    break;

                case ParseableDictionaryField dict:
                    Ln(sb, $"\"{fieldName}\": {{");
                    _indent++;
                    Ln(sb, "\"type\": \"object\",");
                    Ln(sb, "\"additionalProperties\": {");
                    _indent++;
                    if (dict.Children.Count == 1 && dict.Children[0] is ParseableCustomField singleLeaf)
                        Ln(sb, $"\"type\": \"{GetJsonType(singleLeaf.ValueType)}\"");
                    else
                        WriteObjectProperties(sb, dict.Children);
                    _indent--;
                    Ln(sb, "}");
                    _indent--;
                    Ln(sb, "}" + Trail(isLast));
                    break;
            }
        }

        private void WriteArrayItemSchema(StringBuilder sb, ParseableArrayField arr)
        {
            if (arr.Children.Count == 1 && arr.Children[0] is ParseableCustomField leaf)
            {
                Ln(sb, $"\"type\": \"{GetJsonType(leaf.ValueType)}\"");
            }
            else
            {
                WriteObjectProperties(sb, arr.Children);
            }
        }

        private static string GetJsonType(FieldValueType valueType)
        {
            return valueType switch
            {
                FieldValueType.String => "string",
                FieldValueType.Int => "integer",
                FieldValueType.Float => "number",
                FieldValueType.Bool => "boolean",
                FieldValueType.Enum => "string",
                _ => "object"
            };
        }

        private static string Trail(bool isLast) => isLast ? "" : ",";

        private void Ln(StringBuilder sb, string text)
        {
            sb.Append(' ', _indent * 2);
            sb.AppendLine(text);
        }
    }
}
