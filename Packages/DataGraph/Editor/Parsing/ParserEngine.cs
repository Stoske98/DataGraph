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
        public Result<ParsedDataTree> Parse(RawTableData data, ParseableGraph graph)
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
                    ParseableDictionaryRoot dictRoot => ParseDictionaryRoot(dictRoot, nodeParser, context),
                    ParseableArrayRoot arrayRoot => ParseArrayRoot(arrayRoot, nodeParser, context),
                    ParseableObjectRoot objRoot => ParseObjectRoot(objRoot, nodeParser, context),
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
            ParseContext context)
        {
            var entries = new Dictionary<object, ParsedNode>();
            int currentRow = 0;

            while (currentRow < context.TableData.RowCount)
            {
                var keyRaw = context.TableData.GetCell(currentRow, root.KeyColumn);

                if (string.IsNullOrEmpty(keyRaw))
                {
                    currentRow++;
                    continue;
                }

                object key;
                if (root.KeyType == KeyType.Int)
                {
                    if (!int.TryParse(keyRaw.Trim(), out int intKey))
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
            ParseContext context)
        {
            var elements = new List<ParsedNode>();
            int currentRow = 0;

            while (currentRow < context.TableData.RowCount)
            {
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
        /// Scans forward from startRow to find the next row where the key column
        /// has a non-empty value. Returns TableData.RowCount if none found.
        /// Used to establish maxRow boundaries for child parsers.
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
    }
}
