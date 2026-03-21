using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Validates a ParsedDataTree after parsing.
    /// Checks for structural issues that the parser itself
    /// does not catch (empty collections, missing required data, etc.).
    /// </summary>
    internal sealed class DataValidator
    {
        /// <summary>
        /// Validates the given parsed data tree.
        /// </summary>
        public ValidationReport Validate(ParsedDataTree tree)
        {
            var entries = new List<ValidationEntry>();

            if (tree.Root == null)
            {
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Error, "Parsed data tree has no root."));
                return new ValidationReport(entries);
            }

            ValidateNode(tree.Root, entries);

            entries.AddRange(tree.ParseWarnings);

            return new ValidationReport(entries);
        }

        private void ValidateNode(ParsedNode node, List<ValidationEntry> entries)
        {
            switch (node)
            {
                case ParsedDictionary dict:
                    ValidateDictionary(dict, entries);
                    break;
                case ParsedArray arr:
                    ValidateArray(arr, entries);
                    break;
                case ParsedObject obj:
                    ValidateObject(obj, entries);
                    break;
                case ParsedValue val:
                    ValidateValue(val, entries);
                    break;
                case ParsedAssetReference assetRef:
                    if (string.IsNullOrEmpty(assetRef.AssetPath))
                        entries.Add(new ValidationEntry(
                            ValidationSeverity.Warning,
                            $"Asset field '{assetRef.FieldName}' has empty path."));
                    break;
            }
        }

        private void ValidateDictionary(ParsedDictionary dict, List<ValidationEntry> entries)
        {
            if (dict.Entries.Count == 0)
            {
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Warning,
                    $"Dictionary '{dict.FieldName ?? "root"}' is empty."));
            }

            foreach (var kvp in dict.Entries)
            {
                ValidateNode(kvp.Value, entries);
            }
        }

        private void ValidateArray(ParsedArray arr, List<ValidationEntry> entries)
        {
            if (arr.Elements.Count == 0)
            {
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Info,
                    $"Array '{arr.FieldName ?? "root"}' is empty."));
            }

            foreach (var element in arr.Elements)
            {
                ValidateNode(element, entries);
            }
        }

        private void ValidateObject(ParsedObject obj, List<ValidationEntry> entries)
        {
            foreach (var child in obj.Children)
            {
                ValidateNode(child, entries);
            }
        }

        private void ValidateValue(ParsedValue val, List<ValidationEntry> entries)
        {
            if (val.Value == null && val.ValueType != typeof(string))
            {
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Warning,
                    $"Field '{val.FieldName}' has null value for non-string type {val.ValueType.Name}."));
            }
        }
    }
}
