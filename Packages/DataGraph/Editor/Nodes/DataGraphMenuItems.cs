using UnityEditor;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// Provides Unity menu items for creating DataGraph assets.
    /// </summary>
    internal static class DataGraphMenuItems
    {
        [MenuItem("Assets/Create/DataGraph/Parser Graph", false, 80)]
        private static void CreateDataGraph()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DataGraphAsset>();
        }
    }
}
