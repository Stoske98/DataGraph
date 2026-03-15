using System;
using DataGraph.Editor.Public;

namespace DataGraph.Editor
{
    /// <summary>
    /// Lazily detects available ISheetProvider implementations
    /// through assembly reflection. GoogleSheets is optional.
    /// </summary>
    internal static class ProviderRegistry
    {
        private const string GoogleSheetsTypeName =
            "DataGraph.GoogleSheets.GoogleSheetsProvider, DataGraph.GoogleSheets";

        /// <summary>
        /// Whether the Google Sheets provider assembly is available.
        /// </summary>
        public static bool IsGoogleSheetsAvailable()
        {
            return Type.GetType(GoogleSheetsTypeName) != null;
        }

        /// <summary>
        /// Creates the Google Sheets provider instance.
        /// </summary>
        public static ISheetProvider CreateGoogleSheetsProvider()
        {
            var type = Type.GetType(GoogleSheetsTypeName);
            if (type == null)
                throw new InvalidOperationException(
                    "Google Sheets provider is not installed.");

            return (ISheetProvider)Activator.CreateInstance(type);
        }
    }
}
