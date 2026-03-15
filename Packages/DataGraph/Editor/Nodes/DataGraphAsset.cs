using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Nodes
{
    /// <summary>
    /// The graph asset type for DataGraph parser definitions.
    /// Registered with the .datagraph file extension.
    /// OnGraphChanged triggers real-time validation via GraphLogger.
    /// </summary>
    [Graph("datagraph")]
    [Serializable]
    internal class DataGraphAsset : Graph
    {
        [SerializeField] private string _sheetId = "";
        [SerializeField] private string _sheetName = "Sheet1";
        [SerializeField] private int _headerRowOffset = 1;
        [SerializeField] private string _graphName = "";

        public string SheetId
        {
            get => _sheetId;
            set => _sheetId = value;
        }

        public string SheetName
        {
            get => _sheetName;
            set => _sheetName = value;
        }

        public int HeaderRowOffset
        {
            get => _headerRowOffset;
            set => _headerRowOffset = value;
        }

        public string GraphName
        {
            get => _graphName;
            set => _graphName = value;
        }

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            var validator = new Adapter.GraphValidator(graphLogger);
            validator.Validate(this);
        }
    }
}
