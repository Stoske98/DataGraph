using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Serialization
{
    /// <summary>
    /// Serializes a ParsedDataTree into a JSON string.
    /// Produces a data-only file — no C# classes are generated.
    /// </summary>
    internal sealed class JsonDataSerializer
    {
        private readonly bool _prettyPrint;
        private readonly NullHandling _nullHandling;

        /// <summary>
        /// How null values are handled in the JSON output.
        /// </summary>
        internal enum NullHandling
        {
            Omit,
            IncludeAsNull
        }

        public JsonDataSerializer(bool prettyPrint = true, NullHandling nullHandling = NullHandling.Omit)
        {
            _prettyPrint = prettyPrint;
            _nullHandling = nullHandling;
        }

        /// <summary>
        /// Serializes the parsed data tree to a JSON string.
        /// </summary>
        public Result<string> Serialize(ParsedDataTree tree)
        {
            if (tree?.Root == null)
                return Result<string>.Failure("Parsed data tree or root is null.");

            try
            {
                var sb = new StringBuilder();
                WriteNode(sb, tree.Root, 0);
                if (_prettyPrint) sb.AppendLine();
                return Result<string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"JSON serialization failed: {ex.Message}");
            }
        }

        private void WriteNode(StringBuilder sb, ParsedNode node, int indent)
        {
            switch (node)
            {
                case ParsedDictionary dict:
                    WriteDictionary(sb, dict, indent);
                    break;
                case ParsedArray arr:
                    WriteArray(sb, arr, indent);
                    break;
                case ParsedObject obj:
                    WriteObject(sb, obj, indent);
                    break;
                case ParsedValue val:
                    WriteValue(sb, val);
                    break;
                case ParsedAssetReference assetRef:
                    WriteJsonString(sb, assetRef.AssetPath ?? "");
                    break;
            }
        }

        private void WriteDictionary(StringBuilder sb, ParsedDictionary dict, int indent)
        {
            sb.Append('{');
            bool first = true;

            foreach (var kvp in dict.Entries)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indent + 1);
                WriteJsonString(sb, kvp.Key.ToString());
                sb.Append(':');
                if (_prettyPrint) sb.Append(' ');
                WriteNode(sb, kvp.Value, indent + 1);
            }

            if (!first) NewLine(sb, indent);
            sb.Append('}');
        }

        private void WriteArray(StringBuilder sb, ParsedArray arr, int indent)
        {
            sb.Append('[');
            bool first = true;

            foreach (var element in arr.Elements)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indent + 1);
                WriteNode(sb, element, indent + 1);
            }

            if (!first) NewLine(sb, indent);
            sb.Append(']');
        }

        private void WriteObject(StringBuilder sb, ParsedObject obj, int indent)
        {
            sb.Append('{');
            bool first = true;

            foreach (var child in obj.Children)
            {
                if (child is ParsedValue pv && pv.Value == null && _nullHandling == NullHandling.Omit)
                    continue;
                if (child is ParsedAssetReference ar && string.IsNullOrEmpty(ar.AssetPath) && _nullHandling == NullHandling.Omit)
                    continue;

                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indent + 1);
                WriteJsonString(sb, child.FieldName ?? "");
                sb.Append(':');
                if (_prettyPrint) sb.Append(' ');
                WriteNode(sb, child, indent + 1);
            }

            if (!first) NewLine(sb, indent);
            sb.Append('}');
        }

        private void WriteValue(StringBuilder sb, ParsedValue val)
        {
            if (val.Value == null)
            {
                sb.Append("null");
                return;
            }

            switch (val.Value)
            {
                case string s:
                    WriteJsonString(sb, s);
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(f.ToString("G", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("G", CultureInfo.InvariantCulture));
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case UnityEngine.Vector2 v2:
                    sb.Append($"{{\"x\":{Format(v2.x)},\"y\":{Format(v2.y)}}}");
                    break;
                case UnityEngine.Vector3 v3:
                    sb.Append($"{{\"x\":{Format(v3.x)},\"y\":{Format(v3.y)},\"z\":{Format(v3.z)}}}");
                    break;
                case UnityEngine.Color c:
                    sb.Append($"{{\"r\":{Format(c.r)},\"g\":{Format(c.g)},\"b\":{Format(c.b)},\"a\":{Format(c.a)}}}");
                    break;
                case Enum e:
                    WriteJsonString(sb, e.ToString());
                    break;
                default:
                    WriteJsonString(sb, val.Value.ToString());
                    break;
            }
        }

        private static string Format(float f) => f.ToString("G", CultureInfo.InvariantCulture);

        private static void WriteJsonString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
        }

        private void NewLine(StringBuilder sb, int indent)
        {
            if (_prettyPrint)
            {
                sb.AppendLine();
                sb.Append(' ', indent * 2);
            }
        }
    }
}
