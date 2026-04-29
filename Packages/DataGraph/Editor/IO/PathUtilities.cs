using System.IO;

namespace DataGraph.Editor.IO
{
    /// <summary>
    /// File-system path helpers used across the parse pipeline.
    /// </summary>
    internal static class PathUtilities
    {
        /// <summary>
        /// Ensures the directory containing <paramref name="filePath"/> exists,
        /// creating it (and any missing parents) if necessary. Does nothing
        /// if the path has no directory component.
        /// </summary>
        public static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
