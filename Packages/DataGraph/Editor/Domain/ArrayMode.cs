namespace DataGraph.Editor.Domain
{
    /// <summary>
    /// Determines how an array field reads its elements from the table.
    /// </summary>
    internal enum ArrayMode
    {
        /// <summary>
        /// Reads multiple values from a single cell, split by a separator.
        /// Supports only primitive types.
        /// </summary>
        Horizontal,

        /// <summary>
        /// Reads elements spread across multiple rows, tracked by an index column.
        /// Supports primitive and structural types with unlimited nesting.
        /// </summary>
        Vertical
    }
}
