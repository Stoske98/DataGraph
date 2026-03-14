namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Abstract base for all nodes in the ParsedDataTree.
    /// Represents a parsed and type-coerced piece of data.
    /// </summary>
    internal abstract class ParsedNode
    {
        protected ParsedNode(string fieldName)
        {
            FieldName = fieldName;
        }

        /// <summary>
        /// Name of the field this data represents.
        /// Null for root-level containers.
        /// </summary>
        public string FieldName { get; }
    }
}
