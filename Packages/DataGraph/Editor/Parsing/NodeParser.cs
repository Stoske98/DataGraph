using System;
using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Recursive parser that processes individual ParseableNode types
    /// into ParsedNode results. Handles flat fields, horizontal arrays,
    /// vertical arrays, nested objects, and dictionary fields.
    /// All multi-row parsers respect a maxRow boundary to stay within
    /// their parent element's row range.
    /// </summary>
    internal sealed class NodeParser
    {
        private readonly ParseContext _context;

        public NodeParser(ParseContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Parses an object's child fields starting at the given row.
        /// maxRow is the exclusive upper bound — children must not read beyond it.
        /// Returns the parsed children and the total depth consumed.
        /// </summary>
        public ElementParseResult ParseObjectChildren(
            string typeName,
            string fieldName,
            IReadOnlyList<ParseableNode> childDefinitions,
            int startRow,
            int maxRow)
        {
            var children = new List<ParsedNode>();
            int maxDepth = 1;

            foreach (var childDef in childDefinitions)
            {
                var result = ParseNode(childDef, startRow, maxRow);
                children.Add(result.Node);
                if (result.Depth > maxDepth)
                    maxDepth = result.Depth;
            }

            var obj = new ParsedObject(fieldName, typeName, children);
            return new ElementParseResult(obj, maxDepth);
        }

        /// <summary>
        /// Dispatches parsing to the appropriate handler based on node type.
        /// </summary>
        public ElementParseResult ParseNode(ParseableNode definition, int startRow, int maxRow)
        {
            return definition switch
            {
                ParseableCustomField custom => ParseCustomField(custom, startRow),
                ParseableAssetField asset => ParseAssetField(asset, startRow),
                ParseableObjectField obj => ParseObjectField(obj, startRow, maxRow),
                ParseableArrayField arr => ParseArrayField(arr, startRow, maxRow),
                ParseableDictionaryField dict => ParseDictionaryField(dict, startRow, maxRow),
                _ => throw new InvalidOperationException(
                    $"Unexpected node type in parser: {definition.GetType().Name}")
            };
        }

        private ElementParseResult ParseCustomField(ParseableCustomField field, int row)
        {
            var raw = _context.TableData.GetCell(row, field.Column);
            var coerced = ValueCoercer.Coerce(raw, field.ValueType, field);

            if (coerced.IsFailure)
            {
                _context.AddWarning(
                    $"Type coercion failed for field '{field.FieldName}': {coerced.Error}",
                    cell: new CellReference(row, field.Column));

                var defaultVal = ValueCoercer.Coerce("", field.ValueType, field);
                var fallback = defaultVal.IsSuccess ? defaultVal.Value : null;
                var fallbackType = ValueCoercer.GetSystemType(field.ValueType, field.EnumType);
                return new ElementParseResult(
                    new ParsedValue(field.FieldName, fallback, fallbackType), 1);
            }

            var valueType = ValueCoercer.GetSystemType(field.ValueType, field.EnumType);
            return new ElementParseResult(
                new ParsedValue(field.FieldName, coerced.Value, valueType), 1);
        }

        private ElementParseResult ParseAssetField(ParseableAssetField field, int row)
        {
            var raw = _context.TableData.GetCell(row, field.Column);
            return new ElementParseResult(
                new ParsedValue(field.FieldName, raw, typeof(string)), 1);
        }

        private ElementParseResult ParseObjectField(ParseableObjectField field, int row, int maxRow)
        {
            return ParseObjectChildren(field.TypeName, field.FieldName, field.Children, row, maxRow);
        }

        private ElementParseResult ParseArrayField(ParseableArrayField field, int startRow, int maxRow)
        {
            return field.Mode switch
            {
                ArrayMode.Horizontal => ParseHorizontalArray(field, startRow),
                ArrayMode.Vertical => ParseVerticalArray(field, startRow, maxRow),
                _ => throw new InvalidOperationException($"Unknown array mode: {field.Mode}")
            };
        }

        private ElementParseResult ParseHorizontalArray(ParseableArrayField field, int row)
        {
            if (field.Children.Count == 0)
            {
                return new ElementParseResult(
                    new ParsedArray(field.FieldName, field.TypeName, Array.Empty<ParsedNode>()), 1);
            }

            var elementDef = field.Children[0] as ParseableCustomField;
            if (elementDef == null)
            {
                _context.AddWarning(
                    $"Horizontal array '{field.FieldName}' child must be a CustomFieldNode.");
                return new ElementParseResult(
                    new ParsedArray(field.FieldName, field.TypeName, Array.Empty<ParsedNode>()), 1);
            }

            var raw = _context.TableData.GetCell(row, elementDef.Column);
            if (string.IsNullOrEmpty(raw))
            {
                return new ElementParseResult(
                    new ParsedArray(field.FieldName, field.TypeName, Array.Empty<ParsedNode>()), 1);
            }

            var separator = field.Separator ?? ",";
            var parts = raw.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            var elements = new List<ParsedNode>();

            for (int i = 0; i < parts.Length; i++)
            {
                var coerced = ValueCoercer.Coerce(parts[i].Trim(), elementDef.ValueType, elementDef);
                if (coerced.IsSuccess)
                {
                    var valueType = ValueCoercer.GetSystemType(elementDef.ValueType, elementDef.EnumType);
                    elements.Add(new ParsedValue(null, coerced.Value, valueType));
                }
                else
                {
                    _context.AddWarning(
                        $"Horizontal array '{field.FieldName}' element [{i}] coercion failed: {coerced.Error}",
                        cell: new CellReference(row, elementDef.Column));
                }
            }

            return new ElementParseResult(
                new ParsedArray(field.FieldName, field.TypeName, elements), 1);
        }

        private ElementParseResult ParseVerticalArray(ParseableArrayField field, int startRow, int maxRow)
        {
            var elements = new List<ParsedNode>();
            int currentRow = startRow;
            int previousIndex = -1;

            while (currentRow < maxRow)
            {
                var indexRaw = _context.TableData.GetCell(currentRow, field.IndexColumn);

                if (string.IsNullOrEmpty(indexRaw))
                    break;

                if (!int.TryParse(indexRaw.Trim(), out int currentIndex))
                    break;

                if (currentIndex < previousIndex)
                    break;

                previousIndex = currentIndex;

                if (field.Children.Count == 1 && field.Children[0] is ParseableCustomField singleLeaf)
                {
                    var leafResult = ParseCustomField(singleLeaf, currentRow);
                    elements.Add(leafResult.Node);
                    currentRow += 1;
                }
                else
                {
                    var elementResult = ParseObjectChildren(
                        field.TypeName, null, field.Children, currentRow, maxRow);
                    elements.Add(elementResult.Node);
                    currentRow += elementResult.Depth;
                }
            }

            int totalDepth = currentRow - startRow;
            if (totalDepth < 1) totalDepth = 1;

            return new ElementParseResult(
                new ParsedArray(field.FieldName, field.TypeName, elements), totalDepth);
        }

        private ElementParseResult ParseDictionaryField(ParseableDictionaryField field, int startRow, int maxRow)
        {
            var entries = new Dictionary<object, ParsedNode>();
            int currentRow = startRow;

            while (currentRow < maxRow)
            {
                var keyRaw = _context.TableData.GetCell(currentRow, field.KeyColumn);

                if (string.IsNullOrEmpty(keyRaw))
                    break;

                object key;
                if (field.KeyType == KeyType.Int)
                {
                    if (!int.TryParse(keyRaw.Trim(), out int intKey))
                        break;
                    key = intKey;
                }
                else
                {
                    key = keyRaw.Trim();
                }

                if (entries.ContainsKey(key))
                {
                    _context.AddWarning(
                        $"Duplicate key '{key}' in dictionary field '{field.FieldName}' at row {currentRow}.",
                        cell: new CellReference(currentRow, field.KeyColumn));
                    currentRow++;
                    continue;
                }

                if (field.Children.Count == 1 && field.Children[0] is ParseableCustomField singleLeaf)
                {
                    var leafResult = ParseCustomField(singleLeaf, currentRow);
                    entries[key] = leafResult.Node;
                    currentRow += 1;
                }
                else
                {
                    var valueResult = ParseObjectChildren(
                        field.TypeName, null, field.Children, currentRow, maxRow);
                    entries[key] = valueResult.Node;
                    currentRow += valueResult.Depth;
                }
            }

            int totalDepth = currentRow - startRow;
            if (totalDepth < 1) totalDepth = 1;

            var keyTypeName = field.KeyType == KeyType.Int ? "int" : "string";
            return new ElementParseResult(
                new ParsedDictionary(field.FieldName, keyTypeName, field.TypeName, entries),
                totalDepth);
        }
    }
}
