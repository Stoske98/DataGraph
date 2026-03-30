using System;
using DataGraph.Editor.Public;

namespace DataGraph.Editor
{
    /// <summary>
    /// Lazily detects available ISheetProvider implementations
    /// through assembly reflection. Each provider is in its own
    /// optional assembly: GoogleSheets, LocalFile, OneDrive.
    /// Also detects optional output format assemblies (Blob, Quantum).
    /// </summary>
    internal static class ProviderRegistry
    {
        private const string GoogleSheetsTypeName =
            "DataGraph.GoogleSheets.GoogleSheetsProvider, DataGraph.GoogleSheets";
        private const string LocalFileTypeName =
            "DataGraph.LocalFile.LocalFileProvider, DataGraph.LocalFile";
        private const string OneDriveTypeName =
            "DataGraph.OneDrive.OneDriveProvider, DataGraph.OneDrive";

        public static bool IsGoogleSheetsAvailable() =>
            Type.GetType(GoogleSheetsTypeName) != null;

        public static ISheetProvider CreateGoogleSheetsProvider()
        {
            var type = Type.GetType(GoogleSheetsTypeName)
                ?? throw new InvalidOperationException(
                    "Google Sheets provider is not installed.");
            return (ISheetProvider)Activator.CreateInstance(type);
        }

        public static bool IsLocalFileAvailable() =>
            Type.GetType(LocalFileTypeName) != null;

        public static ISheetProvider CreateLocalFileProvider()
        {
            var type = Type.GetType(LocalFileTypeName)
                ?? throw new InvalidOperationException(
                    "Local File provider is not installed.");
            return (ISheetProvider)Activator.CreateInstance(type);
        }

        public static bool IsOneDriveAvailable() =>
            Type.GetType(OneDriveTypeName) != null;

        public static ISheetProvider CreateOneDriveProvider()
        {
            var type = Type.GetType(OneDriveTypeName)
                ?? throw new InvalidOperationException(
                    "OneDrive provider is not installed.");
            return (ISheetProvider)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Returns true if the SheetId matches OneDrive URL patterns.
        /// Used by DataGraphWindow for provider auto-detection.
        /// </summary>
        public static bool IsOneDrivePath(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId)) return false;
            if (sheetId.StartsWith("onedrive://")) return true;
            if (sheetId.Contains("1drv.ms")) return true;
            if (sheetId.Contains("sharepoint.com")) return true;
            if (sheetId.Contains("onedrive.live.com")) return true;
            return false;
        }

        public static bool IsBlobAvailable() =>
            Type.GetType("Unity.Entities.BlobBuilder, Unity.Entities") != null;

        public static bool IsQuantumAvailable() =>
            Type.GetType("Quantum.AssetObject, Quantum.Engine") != null;
    }
}
