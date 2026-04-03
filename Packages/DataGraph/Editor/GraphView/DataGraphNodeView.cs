using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Visual representation of a DataGraph node in the GraphView.
    /// Ports use standard horizontal orientation in inputContainer/outputContainer.
    /// Title is fixed per node type and does not change with property edits.
    /// </summary>
    internal sealed class DataGraphNodeView : Node
    {
        private readonly SerializedNode _nodeData;
        private readonly DataGraphView _graphView;
        private readonly Dictionary<string, Port> _inputPorts = new();
        private readonly Dictionary<string, Port> _outputPorts = new();

        public string NodeGuid => _nodeData.Guid;
        public string NodeTypeName => _nodeData.TypeName;

        public DataGraphNodeView(SerializedNode nodeData, DataGraphView graphView)
        {
            _nodeData = nodeData;
            _graphView = graphView;

            title = NodeTypeRegistry.GetDisplayTitle(nodeData.TypeName);
            SetPosition(new Rect(nodeData.Position, new Vector2(200, 0)));

            ApplyHeaderColor();
            ApplyNodeStyle();
            CreatePorts();
            CreatePropertyControls();

            RefreshExpandedState();
            RefreshPorts();
        }

        public Port GetOutputPort(string portName)
        {
            _outputPorts.TryGetValue(portName, out var port);
            return port;
        }

        public Port GetInputPort(string portName)
        {
            _inputPorts.TryGetValue(portName, out var port);
            return port;
        }

        private void ApplyHeaderColor()
        {
            var color = NodeTypeRegistry.GetNodeColor(_nodeData.TypeName);
            titleContainer.style.backgroundColor = color;
        }

        private void ApplyNodeStyle()
        {
            // Node body background
            mainContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            // Extension container background (where property controls live)
            extensionContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Border
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderRightColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        }

        private void CreatePorts()
        {
            if (NodeTypeRegistry.HasParentPort(_nodeData.TypeName))
            {
                var inputPort = InstantiatePort(Orientation.Horizontal,
                    Direction.Input, Port.Capacity.Single, typeof(bool));
                inputPort.portName = "Parent";
                _inputPorts["Parent"] = inputPort;
                inputContainer.Add(inputPort);
            }

            if (NodeTypeRegistry.HasChildrenPort(_nodeData.TypeName))
            {
                var outputPort = InstantiatePort(Orientation.Horizontal,
                    Direction.Output, Port.Capacity.Multi, typeof(bool));
                outputPort.portName = "Children";
                _outputPorts["Children"] = outputPort;
                outputContainer.Add(outputPort);
            }
        }

        private void CreatePropertyControls()
        {
            var container = new VisualElement();
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;

            switch (_nodeData.TypeName)
            {
                // === ROOTS — TypeName comes from graph name, no editable field ===
                case NodeTypeRegistry.Types.DictionaryRoot:
                    AddColumnDropdown(container, "KeyColumn", "Key Column");
                    AddEnumDropdown(container, "KeyType", "Key Type", new List<string> { "Int", "String" });
                    break;

                case NodeTypeRegistry.Types.ArrayRoot:
                case NodeTypeRegistry.Types.ObjectRoot:
                    break;

                case NodeTypeRegistry.Types.EnumRoot:
                case NodeTypeRegistry.Types.FlagRoot:
                    AddColumnDropdown(container, "NameColumn", "Name Column");
                    AddColumnDropdown(container, "ValueColumn", "Value Column");
                    break;

                // === STRUCTURES (TypeName first, then FieldName) ===
                case NodeTypeRegistry.Types.ObjectField:
                    AddTextField(container, "TypeName", "Type Name");
                    AddTextField(container, "FieldName", "Field Name");
                    break;

                case NodeTypeRegistry.Types.VerticalArrayField:
                    AddTextField(container, "TypeName", "Type Name");
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "IndexColumn", "Index Column");
                    break;

                case NodeTypeRegistry.Types.HorizontalArrayField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddSeparatorDropdown(container, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.DictionaryField:
                    AddTextField(container, "TypeName", "Type Name");
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "KeyColumn", "Key Column");
                    AddEnumDropdown(container, "KeyType", "Key Type", new List<string> { "Int", "String" });
                    break;

                // === LEAVES ===
                case NodeTypeRegistry.Types.StringField:
                case NodeTypeRegistry.Types.BoolField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    break;

                case NodeTypeRegistry.Types.NumberField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddEnumDropdown(container, "NumberType", "Number Type",
                        new List<string> { "Int", "Float", "Double" });
                    break;

                case NodeTypeRegistry.Types.Vector2Field:
                case NodeTypeRegistry.Types.Vector3Field:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddSeparatorDropdown(container, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.ColorField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddEnumDropdown(container, "Format", "Format",
                        new List<string> { "Hex", "RGBA" });
                    break;

                case NodeTypeRegistry.Types.AssetField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddEnumDropdown(container, "AssetType", "Asset Type",
                        new List<string> { "Sprite", "Texture2D", "AudioClip", "GameObject",
                            "Material", "AnimationClip", "RuntimeAnimatorController",
                            "ScriptableObject", "Mesh", "Font", "TextAsset" });
                    break;

                case NodeTypeRegistry.Types.EnumField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddGeneratedEnumDropdown(container, "EnumTypeName", "Enum Type", isFlag: false);
                    break;

                case NodeTypeRegistry.Types.FlagField:
                    AddTextField(container, "FieldName", "Field Name");
                    AddColumnDropdown(container, "Column", "Column");
                    AddGeneratedEnumDropdown(container, "FlagTypeName", "Flag Type", isFlag: true);
                    AddSeparatorDropdown(container, "Separator", "Separator");
                    break;
            }

            extensionContainer.Add(container);
        }

        private void AddTextField(VisualElement container, string propKey, string label)
        {
            var current = _nodeData.GetProperty(propKey, "");
            var field = new TextField(label) { value = current };
            field.style.minWidth = 180;
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Property");
                _nodeData.SetProperty(propKey, evt.newValue);
                MarkDirtyDeferred();
            });
            container.Add(field);
        }

        private void AddColumnDropdown(VisualElement container, string propKey, string label)
        {
            var choices = _graphView.GraphAsset?.GetColumnChoices() ?? new List<string> { "A" };
            var current = _nodeData.GetProperty(propKey, choices.Count > 0 ? choices[0] : "A");

            if (!choices.Contains(current))
                choices.Insert(0, current);

            var field = new PopupField<string>(label, choices, current);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Column");
                _nodeData.SetProperty(propKey, evt.newValue);
                MarkDirty();
            });
            container.Add(field);
        }

        private void AddSeparatorDropdown(VisualElement container, string propKey, string label)
        {
            var options = new List<string>(NodeTypeRegistry.SeparatorOptions);
            var current = _nodeData.GetProperty(propKey, ",");

            if (!options.Contains(current))
                options.Insert(0, current);

            var field = new PopupField<string>(label, options, current);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Separator");
                _nodeData.SetProperty(propKey, evt.newValue);
                MarkDirty();
            });
            container.Add(field);
        }

        private void AddEnumDropdown(VisualElement container, string propKey, string label,
            List<string> options)
        {
            var current = _nodeData.GetProperty(propKey, options[0]);
            if (!options.Contains(current))
                options.Insert(0, current);

            var field = new PopupField<string>(label, options, current);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Option");
                _nodeData.SetProperty(propKey, evt.newValue);
                MarkDirty();
            });
            container.Add(field);
        }

        private void AddGeneratedEnumDropdown(VisualElement container, string propKey,
            string label, bool isFlag)
        {
            var choices = ScanGeneratedEnums(isFlag);
            var current = _nodeData.GetProperty(propKey, "");

            if (choices.Count == 0)
                choices.Add("(none generated)");
            if (!string.IsNullOrEmpty(current) && !choices.Contains(current))
                choices.Insert(0, current);

            var field = new PopupField<string>(label, choices,
                string.IsNullOrEmpty(current) ? choices[0] : current);

            if (choices.Count > 0)
            {
                _nodeData.SetProperty(propKey, choices[0]);
            }

            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "(none generated)") return;
                RecordUndo("Change Enum Type");
                _nodeData.SetProperty(propKey, evt.newValue);
                MarkDirty();
            });
            container.Add(field);
        }

        private static List<string> ScanGeneratedEnums(bool isFlag)
        {
            var result = new List<string>();
            var basePath = DataGraphSettings.Instance.Paths.GeneratedFolder;
            var subfolder = isFlag ? "Flags" : "Enums";
            var searchPath = $"{basePath}/{subfolder}";

            if (!AssetDatabase.IsValidFolder(searchPath))
                return result;

            var guids = AssetDatabase.FindAssets("t:MonoScript",
                new[] { searchPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(fileName))
                    result.Add(fileName);
            }
            return result;
        }

        private void RecordUndo(string message)
        {
            if (_graphView.GraphAsset != null)
                Undo.RecordObject(_graphView.GraphAsset, message);
        }

        private void MarkDirty()
        {
            if (_graphView.GraphAsset != null)
                EditorUtility.SetDirty(_graphView.GraphAsset);
            _graphView.NotifyPropertyChanged();
        }

        private void MarkDirtyDeferred()
        {
            if (_graphView.GraphAsset != null)
                EditorUtility.SetDirty(_graphView.GraphAsset);
            _graphView.NotifyPropertyChangedDeferred();
        }
    }
}
