using System;
using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Entry point for parsing raw table data using a graph definition.
    /// Takes RawTableData + ParseableGraph, produces ParsedDataTree.
    /// Combines parsing and type coercion in a single pass.
    /// </summary>
    internal sealed class ParserEngine
    {
        /// <summary>
        /// Parses the given table data according to the graph definition.
        /// </summary>
        public Result<ParsedDataTree> Parse(RawTableData data, ParseableGraph graph, int maxEntries = 0)
        {
            if (data == null)
                return Result<ParsedDataTree>.Failure("Table data is null.");
            if (graph == null)
                return Result<ParsedDataTree>.Failure("Graph is null.");

            var context = new ParseContext(data);
            var nodeParser = new NodeParser(context);

            ParsedNode root;
            try
            {
                root = graph.Root switch
                {
                    ParseableDictionaryRoot dictRoot => ParseDictionaryRoot(dictRoot, nodeParser, context, maxEntries),
                    ParseableArrayRoot arrayRoot => ParseArrayRoot(arrayRoot, nodeParser, context, maxEntries),
                    ParseableObjectRoot objRoot => ParseObjectRoot(objRoot, nodeParser, context),
                    ParseableEnumRoot enumRoot => ParseEnumRoot(enumRoot, context),
                    ParseableFlagRoot flagRoot => ParseFlagRoot(flagRoot, context),
                    _ => throw new InvalidOperationException(
                        $"Unknown root node type: {graph.Root.GetType().Name}")
                };
            }
            catch (Exception ex)
            {
                return Result<ParsedDataTree>.Failure($"Parse error: {ex.Message}");
            }

            var tree = new ParsedDataTree(root, graph, new List<ValidationEntry>(context.Warnings));
            return Result<ParsedDataTree>.Success(tree);
        }

        private ParsedNode ParseDictionaryRoot(
            ParseableDictionaryRoot root,
            NodeParser nodeParser,
            ParseContext context,
            int maxEntries)
        {
            var entries = new Dictionary<object, ParsedNode>();
            int currentRow = 0;

            while (currentRow < context.TableData.RowCount)
            {
                if (maxEntries > 0 && entries.Count >= maxEntries)
                    break;

                var keyRaw = context.TableData.GetCell(currentRow, root.KeyColumn);

                if (string.IsNullOrEmpty(keyRaw))
                {
                    currentRow++;
                    continue;
                }

                object key;
                if (root.KeyType == KeyType.Int)
                {
                    if (!TryParseIntKey(keyRaw.Trim(), out int intKey))
                    {
                        context.AddWarning(
                            $"Cannot parse key '{keyRaw}' as int at row {currentRow}.",
                            cell: new CellReference(currentRow, root.KeyColumn));
                        currentRow++;
                        continue;
                    }
                    key = intKey;
                }
                else
                {
                    key = keyRaw.Trim();
                }

                if (entries.ContainsKey(key))
                {
                    context.AddWarning(
                        $"Duplicate key '{key}' at row {currentRow}, skipping.",
                        cell: new CellReference(currentRow, root.KeyColumn));
                    currentRow++;
                    continue;
                }

                int maxRow = FindNextKeyRow(context.TableData, root.KeyColumn, currentRow + 1);
                var result = nodeParser.ParseObjectChildren(
                    root.TypeName, null, root.Children, currentRow, maxRow);
                entries[key] = result.Node;
                currentRow += result.Depth;
            }

            var keyTypeName = root.KeyType == KeyType.Int ? "int" : "string";
            return new ParsedDictionary(null, keyTypeName, root.TypeName, entries);
        }

        private ParsedNode ParseArrayRoot(
            ParseableArrayRoot root,
            NodeParser nodeParser,
            ParseContext context,
            int maxEntries)
        {
            var elements = new List<ParsedNode>();
            int currentRow = 0;

            while (currentRow < context.TableData.RowCount)
            {
                if (maxEntries > 0 && elements.Count >= maxEntries)
                    break;

                var result = nodeParser.ParseObjectChildren(
                    root.TypeName, null, root.Children, currentRow, context.TableData.RowCount);
                elements.Add(result.Node);
                currentRow += result.Depth;
            }

            return new ParsedArray(null, root.TypeName, elements);
        }

        private ParsedNode ParseObjectRoot(
            ParseableObjectRoot root,
            NodeParser nodeParser,
            ParseContext context)
        {
            if (context.TableData.RowCount == 0)
            {
                context.AddWarning("Object root has no data rows.");
                return new ParsedObject(null, root.TypeName, Array.Empty<ParsedNode>());
            }

            var result = nodeParser.ParseObjectChildren(
                root.TypeName, null, root.Children, 0, context.TableData.RowCount);
            return result.Node;
        }

        /// <summary>
        /// Parses an EnumRoot graph. Reads name and value from each row
        /// to produce a ParsedEnumDefinition with ordered members.
        /// </summary>
        private ParsedNode ParseEnumRoot(ParseableEnumRoot root, ParseContext context)
        {
            var members = ParseEnumMembers(
                root.TypeName, root.NameColumn, root.ValueColumn, context, isFlags: false);
            return new ParsedEnumDefinition(root.TypeName, false, members);
        }

        /// <summary>
        /// Parses a FlagRoot graph. Same as EnumRoot but marks result as [Flags].
        /// </summary>
        private ParsedNode ParseFlagRoot(ParseableFlagRoot root, ParseContext context)
        {
            var members = ParseEnumMembers(
                root.TypeName, root.NameColumn, root.ValueColumn, context, isFlags: true);
            return new ParsedEnumDefinition(root.TypeName, true, members);
        }

        private List<EnumMember> ParseEnumMembers(
            string typeName, string nameColumn, string valueColumn,
            ParseContext context, bool isFlags)
        {
            var members = new List<EnumMember>();
            var usedNames = new HashSet<string>();
            int autoValue = isFlags ? 1 : 0;

            for (int row = 0; row < context.TableData.RowCount; row++)
            {
                var nameRaw = context.TableData.GetCell(row, nameColumn)?.Trim();
                if (string.IsNullOrEmpty(nameRaw))
                    continue;

                var sanitized = SanitizeEnumMemberName(nameRaw);
                if (string.IsNullOrEmpty(sanitized))
                {
                    context.AddWarning(
                        $"Enum '{typeName}': invalid member name '{nameRaw}' at row {row}, skipping.",
                        cell: new CellReference(row, nameColumn));
                    continue;
                }

                if (!usedNames.Add(sanitized))
                {
                    context.AddWarning(
                        $"Enum '{typeName}': duplicate member '{sanitized}' at row {row}, skipping.",
                        cell: new CellReference(row, nameColumn));
                    continue;
                }

                int value;
                var valueRaw = context.TableData.GetCell(row, valueColumn)?.Trim();
                if (!string.IsNullOrEmpty(valueRaw))
                {
                    if (!TryParseIntKey(valueRaw, out value))
                    {
                        context.AddWarning(
                            $"Enum '{typeName}': cannot parse value '{valueRaw}' for '{sanitized}' at row {row}, using auto.",
                            cell: new CellReference(row, valueColumn));
                        value = autoValue;
                    }
                }
                else
                {
                    value = autoValue;
                }

                members.Add(new EnumMember(sanitized, value));

                if (isFlags)
                    autoValue = value == 0 ? 1 : value * 2;
                else
                    autoValue = value + 1;
            }

            return members;
        }

        /// <summary>
        /// Sanitizes a string to be a valid C# identifier.
        /// Removes invalid characters, ensures it starts with a letter or underscore.
        /// </summary>
        private static string SanitizeEnumMemberName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            var sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c == ' ' || c == '-')
                {
                    if (i + 1 < raw.Length && char.IsLetter(raw[i + 1]))
                    {
                        sb.Append(char.ToUpperInvariant(raw[i + 1]));
                        i++;
                    }
                    continue;
                }

                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
            }

            if (sb.Length == 0) return null;
            if (char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        /// <summary>
        /// Scans forward from startRow to find the next row where the key column
        /// has a non-empty value. Returns TableData.RowCount if none found.
        /// </summary>
        private static int FindNextKeyRow(RawTableData data, string keyColumn, int startRow)
        {
            for (int row = startRow; row < data.RowCount; row++)
            {
                var val = data.GetCell(row, keyColumn);
                if (!string.IsNullOrEmpty(val))
                    return row;
            }
            return data.RowCount;
        }

        /// <summary>
        /// Parses a string as int, tolerating floating-point format (e.g. "1.0" from XLSX).
        /// </summary>
        internal static bool TryParseIntKey(string raw, out int result)
        {
            if (int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out result))
                return true;

            if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            {
                result = (int)d;
                return true;
            }

            result = 0;
            return false;
        }
    }
}
