using System;
using DataGraph.Editor.Public;

namespace DataGraph.Editor
{
    /// <summary>
    /// Lazily detects available ISheetProvider implementations
    /// through assembly reflection. Each provider is in its own
    /// optional assembly.
    /// </summary>
    internal static class ProviderRegistry
    {
        private const string GoogleSheetsTypeName =
            "DataGraph.GoogleSheets.GoogleSheetsProvider, DataGraph.GoogleSheets";
        private const string LocalFileTypeName =
            "DataGraph.LocalFile.LocalFileProvider, DataGraph.LocalFile";
        private const string BlobCodeGeneratorTypeName =
            "DataGraph.Blob.BlobCodeGenerator, DataGraph.Blob";

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

        /// <summary>
        /// Whether the Local File provider assembly is available.
        /// </summary>
        public static bool IsLocalFileAvailable()
        {
            return Type.GetType(LocalFileTypeName) != null;
        }

        /// <summary>
        /// Creates the Local File provider instance.
        /// </summary>
        public static ISheetProvider CreateLocalFileProvider()
        {
            var type = Type.GetType(LocalFileTypeName);
            if (type == null)
                throw new InvalidOperationException(
                    "Local File provider is not installed.");

            return (ISheetProvider)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Whether Blob output is available (requires Unity Entities package).
        /// </summary>
        public static bool IsBlobAvailable()
        {
            return Type.GetType("Unity.Entities.BlobBuilder, Unity.Entities") != null;
        }

        /// <summary>
        /// Whether Quantum output is available (requires Photon Quantum SDK).
        /// </summary>
        public static bool IsQuantumAvailable()
        {
            return Type.GetType("Quantum.AssetObject, Quantum.Simulation") != null;
        }
    }
}
