using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DataGraph.Editor
{
    /// <summary>
    /// Build processor that copies all .blob files from the Generated folder
    /// to StreamingAssets/DataGraph/ before build, and cleans up after.
    /// </summary>
    internal sealed class BlobBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string StreamingSubfolder = "DataGraph";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var registry = FindRegistry();
            if (registry == null || registry.BlobEntries.Count == 0)
                return;

            var targetDir = Path.Combine(Application.streamingAssetsPath, StreamingSubfolder);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var outputPath = EditorPrefs.GetString("DataGraph_OutputPath", "Assets/DataGraph/Generated");
            var blobFiles = Directory.GetFiles(outputPath, "*.blob", SearchOption.AllDirectories);

            foreach (var blobFile in blobFiles)
            {
                var fileName = Path.GetFileName(blobFile);
                var destPath = Path.Combine(targetDir, fileName);
                File.Copy(blobFile, destPath, true);
            }

            AssetDatabase.Refresh();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            var targetDir = Path.Combine(Application.streamingAssetsPath, StreamingSubfolder);
            if (!Directory.Exists(targetDir))
                return;

            var files = Directory.GetFiles(targetDir, "*.blob");
            foreach (var file in files)
                File.Delete(file);

            if (!Directory.EnumerateFileSystemEntries(targetDir).Any())
            {
                Directory.Delete(targetDir);
                var metaFile = targetDir + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }

            if (Directory.Exists(Application.streamingAssetsPath) &&
                !Directory.EnumerateFileSystemEntries(Application.streamingAssetsPath).Any())
            {
                Directory.Delete(Application.streamingAssetsPath);
                var metaFile = Application.streamingAssetsPath + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }

            AssetDatabase.Refresh();
        }

        private static DataGraph.Runtime.DataGraphRegistry FindRegistry()
        {
            var guids = AssetDatabase.FindAssets("t:DataGraphRegistry");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<DataGraph.Runtime.DataGraphRegistry>(path);
        }
    }
}
