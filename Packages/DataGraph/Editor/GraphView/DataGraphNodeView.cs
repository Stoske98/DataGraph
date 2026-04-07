using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Visual representation of a DataGraph node in the GraphView.
    /// v2: Dynamic property controls based on parent context.
    /// </summary>
    internal sealed class DataGraphNodeView : Node
    {
        private readonly SerializedNode _nodeData;
        private readonly DataGraphView _graphView;
        private readonly Dictionary<string, Port> _inputPorts = new();
        private readonly Dictionary<string, Port> _outputPorts = new();
        private VisualElement _propertyContainer;

        public string NodeGuid => _nodeData.Guid;
        public string NodeTypeName => _nodeData.TypeName;
        public bool IsFixed => _nodeData.IsFixed;

        public DataGraphNodeView(SerializedNode nodeData, DataGraphView graphView)
        {
            _nodeData = nodeData;
            _graphView = graphView;
            title = NodeTypeRegistry.GetDisplayTitle(nodeData.TypeName);
            SetPosition(new Rect(nodeData.Position, new Vector2(200, 0)));
            ApplyHeaderColor();
            ApplyNodeStyle();
            CreatePorts();
            BuildPropertyControls();
            RefreshExpandedState();
            RefreshPorts();
            if (nodeData.IsFixed) capabilities &= ~Capabilities.Deletable;
        }

        public Port GetOutputPort(string portName) { _outputPorts.TryGetValue(portName, out var p); return p; }
        public Port GetInputPort(string portName) { _inputPorts.TryGetValue(portName, out var p); return p; }

        public void RefreshPropertyControls()
        {
            if (_propertyContainer != null) extensionContainer.Remove(_propertyContainer);
            BuildPropertyControls();
            RefreshExpandedState();
        }

        private void ApplyHeaderColor() { titleContainer.style.backgroundColor = NodeTypeRegistry.GetNodeColor(_nodeData.TypeName); }

        private void ApplyNodeStyle()
        {
            mainContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            extensionContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            var bc = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = bc;
        }

        private void CreatePorts()
        {
            if (NodeTypeRegistry.HasParentPort(_nodeData.TypeName))
            {
                var p = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
                p.portName = "Parent"; _inputPorts["Parent"] = p; inputContainer.Add(p);
            }
            if (NodeTypeRegistry.HasChildrenPort(_nodeData.TypeName))
            {
                var cap = NodeTypeRegistry.IsMultiChildPort(_nodeData.TypeName) ? Port.Capacity.Multi : Port.Capacity.Single;
                var p = InstantiatePort(Orientation.Horizontal, Direction.Output, cap, typeof(bool));
                p.portName = "Children"; _outputPorts["Children"] = p; outputContainer.Add(p);
            }
        }

        private void BuildPropertyControls()
        {
            _propertyContainer = new VisualElement();
            _propertyContainer.style.paddingLeft = 8;
            _propertyContainer.style.paddingRight = 8;
            _propertyContainer.style.paddingTop = 4;
            _propertyContainer.style.paddingBottom = 4;

            var parentType = _graphView.GraphAsset?.GetParentTypeName(_nodeData.Guid);
            var showFieldName = NodeTypeRegistry.ShouldShowFieldName(parentType);

            switch (_nodeData.TypeName)
            {
                case NodeTypeRegistry.Types.Root:
                    break;

                case NodeTypeRegistry.Types.Dictionary:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "KeyColumn", "Key Column");
                    AddEnumDropdown(_propertyContainer, "KeyType", "Key Type", new List<string> { "Int", "String" });
                    break;

                case NodeTypeRegistry.Types.VerticalArray:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    if (NodeTypeRegistry.ShouldShowIndexColumn(parentType))
                        AddColumnDropdown(_propertyContainer, "IndexColumn", "Index Column");
                    break;

                case NodeTypeRegistry.Types.HorizontalArray:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.Object:
                    AddTextField(_propertyContainer, "TypeName", "Type Name");
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    break;

                case NodeTypeRegistry.Types.Enum:
                    AddTextField(_propertyContainer, "TypeName", "Type Name");
                    AddColumnDropdown(_propertyContainer, "NameColumn", "Name Column");
                    AddColumnDropdown(_propertyContainer, "ValueColumn", "Value Column");
                    break;

                case NodeTypeRegistry.Types.Flag:
                    AddTextField(_propertyContainer, "TypeName", "Type Name");
                    AddColumnDropdown(_propertyContainer, "NameColumn", "Name Column");
                    AddColumnDropdown(_propertyContainer, "ValueColumn", "Value Column");
                    break;

                case NodeTypeRegistry.Types.StringField:
                case NodeTypeRegistry.Types.BoolField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    break;

                case NodeTypeRegistry.Types.NumberField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "NumberType", "Number Type", new List<string> { "Int", "Float", "Double" });
                    break;

                case NodeTypeRegistry.Types.Vector2Field:
                case NodeTypeRegistry.Types.Vector3Field:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.ColorField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "Format", "Format", new List<string> { "Hex", "RGBA" });
                    break;

                case NodeTypeRegistry.Types.AssetField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "AssetType", "Asset Type",
                        new List<string> { "Sprite", "Texture2D", "AudioClip", "GameObject", "Material",
                            "AnimationClip", "RuntimeAnimatorController", "ScriptableObject", "Mesh", "Font", "TextAsset" });
                    break;

                case NodeTypeRegistry.Types.EnumField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddGeneratedEnumDropdown(_propertyContainer, "EnumTypeName", "Enum Type", false);
                    break;

                case NodeTypeRegistry.Types.FlagField:
                    if (showFieldName) AddTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddGeneratedEnumDropdown(_propertyContainer, "FlagTypeName", "Flag Type", true);
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;
            }
            extensionContainer.Add(_propertyContainer);
        }

        private void AddTextField(VisualElement c, string key, string label)
        {
            var field = new TextField(label) { value = _nodeData.GetProperty(key, "") };
            field.style.minWidth = 180;
            field.RegisterValueChangedCallback(evt => { RecordUndo("Change Property"); _nodeData.SetProperty(key, evt.newValue); MarkDirtyDeferred(); });
            c.Add(field);
        }

        private void AddColumnDropdown(VisualElement c, string key, string label)
        {
            var choices = _graphView.GraphAsset?.GetColumnChoices() ?? new List<string> { "A" };
            var cur = _nodeData.GetProperty(key, choices.Count > 0 ? choices[0] : "A");
            if (!choices.Contains(cur)) choices.Insert(0, cur);
            var field = new PopupField<string>(label, choices, cur);
            field.RegisterValueChangedCallback(evt => { RecordUndo("Change Column"); _nodeData.SetProperty(key, evt.newValue); MarkDirty(); });
            c.Add(field);
        }

        private void AddSeparatorDropdown(VisualElement c, string key, string label)
        {
            var opts = new List<string>(NodeTypeRegistry.SeparatorOptions);
            var cur = _nodeData.GetProperty(key, ",");
            if (!opts.Contains(cur)) opts.Insert(0, cur);
            var field = new PopupField<string>(label, opts, cur);
            field.RegisterValueChangedCallback(evt => { RecordUndo("Change Separator"); _nodeData.SetProperty(key, evt.newValue); MarkDirty(); });
            c.Add(field);
        }

        private void AddEnumDropdown(VisualElement c, string key, string label, List<string> opts)
        {
            var cur = _nodeData.GetProperty(key, opts[0]);
            if (!opts.Contains(cur)) opts.Insert(0, cur);
            var field = new PopupField<string>(label, opts, cur);
            field.RegisterValueChangedCallback(evt => { RecordUndo("Change Option"); _nodeData.SetProperty(key, evt.newValue); MarkDirty(); });
            c.Add(field);
        }

        private void AddGeneratedEnumDropdown(VisualElement c, string key, string label, bool isFlag)
        {
            var choices = ScanGeneratedEnums(isFlag);
            var cur = _nodeData.GetProperty(key, "");
            if (choices.Count == 0) choices.Add("(none generated)");
            if (!string.IsNullOrEmpty(cur) && !choices.Contains(cur)) choices.Insert(0, cur);
            var field = new PopupField<string>(label, choices, string.IsNullOrEmpty(cur) ? choices[0] : cur);
            if (choices.Count > 0 && choices[0] != "(none generated)") _nodeData.SetProperty(key, choices[0]);
            field.RegisterValueChangedCallback(evt => { if (evt.newValue == "(none generated)") return; RecordUndo("Change Enum Type"); _nodeData.SetProperty(key, evt.newValue); MarkDirty(); });
            c.Add(field);
        }

        private static List<string> ScanGeneratedEnums(bool isFlag)
        {
            var result = new List<string>();
            var basePath = DataGraphSettings.Instance.Paths.GeneratedFolder;
            var path = $"{basePath}/{(isFlag ? "Flags" : "Enums")}";
            if (!AssetDatabase.IsValidFolder(path)) return result;
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { path }))
            {
                var fn = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!string.IsNullOrEmpty(fn)) result.Add(fn);
            }
            return result;
        }

        private void RecordUndo(string msg) { if (_graphView.GraphAsset != null) Undo.RecordObject(_graphView.GraphAsset, msg); }
        private void MarkDirty() { if (_graphView.GraphAsset != null) EditorUtility.SetDirty(_graphView.GraphAsset); _graphView.NotifyPropertyChanged(); }
        private void MarkDirtyDeferred() { if (_graphView.GraphAsset != null) EditorUtility.SetDirty(_graphView.GraphAsset); _graphView.NotifyPropertyChangedDeferred(); }
    }
}
