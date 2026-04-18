using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DataGraph.Editor.GraphView
{
    /// <summary>
    /// Visual representation of a DataGraph node.
    /// Shows inline warning icon next to empty required fields.
    /// Warning state refreshes on focus-lost, not on every keystroke.
    /// </summary>
    internal sealed class DataGraphNodeView : Node
    {
        private readonly SerializedNode _nodeData;
        private readonly DataGraphView _graphView;
        private readonly Dictionary<string, Port> _inputPorts = new();
        private readonly Dictionary<string, Port> _outputPorts = new();
        private VisualElement _propertyContainer;
        private VisualElement _warningBadge;

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

        public Port GetOutputPort(string n) { _outputPorts.TryGetValue(n, out var p); return p; }
        public Port GetInputPort(string n) { _inputPorts.TryGetValue(n, out var p); return p; }

        public void RefreshPropertyControls()
        {
            if (_propertyContainer != null) extensionContainer.Remove(_propertyContainer);
            RemoveWarningBadge();
            BuildPropertyControls();
            RefreshExpandedState();
        }

        private void RemoveWarningBadge()
        {
            if (_warningBadge != null) { _warningBadge.RemoveFromHierarchy(); _warningBadge = null; }
        }

        private void ApplyHeaderColor()
        {
            titleContainer.style.backgroundColor = NodeTypeRegistry.GetNodeColor(_nodeData.TypeName);
        }

        private void ApplyNodeStyle()
        {
            mainContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            extensionContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            var bc = new Color(0.1f, 0.1f, 0.1f, 1f);
            style.borderTopWidth = style.borderBottomWidth =
                style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopColor = style.borderBottomColor =
                style.borderLeftColor = style.borderRightColor = bc;
        }

        private void CreatePorts()
        {
            if (NodeTypeRegistry.HasParentPort(_nodeData.TypeName))
            {
                var p = InstantiatePort(Orientation.Horizontal,
                    Direction.Input, Port.Capacity.Single, typeof(bool));
                p.portName = "Parent"; _inputPorts["Parent"] = p; inputContainer.Add(p);
            }
            if (NodeTypeRegistry.HasChildrenPort(_nodeData.TypeName))
            {
                var cap = NodeTypeRegistry.IsMultiChildPort(_nodeData.TypeName)
                    ? Port.Capacity.Multi : Port.Capacity.Single;
                var p = InstantiatePort(Orientation.Horizontal,
                    Direction.Output, cap, typeof(bool));
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
            bool hasWarnings = false;

            switch (_nodeData.TypeName)
            {
                case NodeTypeRegistry.Types.Root:
                    break;

                case NodeTypeRegistry.Types.Dictionary:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "KeyColumn", "Key Column");
                    AddEnumDropdown(_propertyContainer, "KeyType", "Key Type",
                        new List<string> { "Int", "String" });
                    break;

                case NodeTypeRegistry.Types.VerticalArray:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    if (NodeTypeRegistry.ShouldShowIndexColumn(parentType))
                        AddColumnDropdown(_propertyContainer, "IndexColumn", "Index Column");
                    break;

                case NodeTypeRegistry.Types.HorizontalArray:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.Object:
                    hasWarnings |= AddRequiredTextField(_propertyContainer, "TypeName", "Type Name");
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    break;

                case NodeTypeRegistry.Types.Enum:
                    hasWarnings |= AddRequiredTextField(_propertyContainer, "TypeName", "Type Name");
                    AddColumnDropdown(_propertyContainer, "NameColumn", "Name Column");
                    AddColumnDropdown(_propertyContainer, "ValueColumn", "Value Column");
                    break;

                case NodeTypeRegistry.Types.Flag:
                    hasWarnings |= AddRequiredTextField(_propertyContainer, "TypeName", "Type Name");
                    AddColumnDropdown(_propertyContainer, "NameColumn", "Name Column");
                    AddColumnDropdown(_propertyContainer, "ValueColumn", "Value Column");
                    break;

                case NodeTypeRegistry.Types.StringField:
                case NodeTypeRegistry.Types.BoolField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    break;

                case NodeTypeRegistry.Types.NumberField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "NumberType", "Number Type",
                        new List<string> { "Int", "Float", "Double" });
                    break;

                case NodeTypeRegistry.Types.Vector2Field:
                case NodeTypeRegistry.Types.Vector3Field:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;

                case NodeTypeRegistry.Types.ColorField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "Format", "Format",
                        new List<string> { "Hex", "RGBA" });
                    break;

                case NodeTypeRegistry.Types.AssetField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddEnumDropdown(_propertyContainer, "AssetType", "Asset Type",
                        new List<string> { "Sprite", "Texture2D", "AudioClip", "GameObject",
                            "Material", "AnimationClip", "RuntimeAnimatorController",
                            "ScriptableObject", "Mesh", "Font", "TextAsset" });
                    break;

                case NodeTypeRegistry.Types.EnumField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddGeneratedEnumDropdown(_propertyContainer, "EnumTypeName", "Enum Type", false);
                    break;

                case NodeTypeRegistry.Types.FlagField:
                    if (showFieldName)
                        hasWarnings |= AddRequiredTextField(_propertyContainer, "FieldName", "Field Name");
                    AddColumnDropdown(_propertyContainer, "Column", "Column");
                    AddGeneratedEnumDropdown(_propertyContainer, "FlagTypeName", "Flag Type", true);
                    AddSeparatorDropdown(_propertyContainer, "Separator", "Separator");
                    break;
            }

            if (hasWarnings)
            {
                _warningBadge = new Label("\u26A0");
                ((Label)_warningBadge).style.fontSize = 14;
                _warningBadge.style.color = new Color(1f, 0.8f, 0.2f);
                _warningBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                _warningBadge.style.marginLeft = 4;
                _warningBadge.tooltip = "Required fields are empty";
                titleContainer.Add(_warningBadge);
            }

            extensionContainer.Add(_propertyContainer);
        }

        /// <summary>
        /// Adds a text field with inline warning icon when empty.
        /// Returns true if field is currently empty (has warning).
        /// Saves on every keystroke but only refreshes warnings on focus-lost.
        /// </summary>
        private bool AddRequiredTextField(VisualElement container, string key, string label)
        {
            var current = _nodeData.GetProperty(key, "");
            bool isEmpty = string.IsNullOrEmpty(current);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var field = new TextField(label) { value = current };
            field.style.flexGrow = 1;
            field.style.minWidth = 160;

            // Warning icon next to field
            var warn = new Label("\u26A0");
            warn.style.fontSize = 12;
            warn.style.color = new Color(1f, 0.8f, 0.2f);
            warn.style.marginLeft = 4;
            warn.style.width = 16;
            warn.style.unityTextAlign = TextAnchor.MiddleCenter;
            warn.tooltip = $"{label} is required";
            warn.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;

            // Save value on every keystroke (no rebuild)
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Property");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirtyDeferred();

                // Toggle warning icon visibility immediately
                warn.style.display = string.IsNullOrEmpty(evt.newValue)
                    ? DisplayStyle.Flex : DisplayStyle.None;
            });

            // Refresh title badge on focus-lost
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                UpdateTitleWarningBadge();
            });

            row.Add(field);
            row.Add(warn);
            container.Add(row);

            return isEmpty;
        }

        /// <summary>
        /// Updates the title bar warning badge based on current field values.
        /// Called on focus-lost to avoid rebuilding during typing.
        /// </summary>
        private void UpdateTitleWarningBadge()
        {
            bool needsWarning = HasAnyEmptyRequiredField();

            if (needsWarning && _warningBadge == null)
            {
                _warningBadge = new Label("\u26A0");
                ((Label)_warningBadge).style.fontSize = 14;
                _warningBadge.style.color = new Color(1f, 0.8f, 0.2f);
                _warningBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                _warningBadge.style.marginLeft = 4;
                _warningBadge.tooltip = "Required fields are empty";
                titleContainer.Add(_warningBadge);
            }
            else if (!needsWarning && _warningBadge != null)
            {
                RemoveWarningBadge();
            }
        }

        private bool HasAnyEmptyRequiredField()
        {
            var parentType = _graphView.GraphAsset?.GetParentTypeName(_nodeData.Guid);
            var showFieldName = NodeTypeRegistry.ShouldShowFieldName(parentType);

            if (_nodeData.TypeName is NodeTypeRegistry.Types.Object
                or NodeTypeRegistry.Types.Enum
                or NodeTypeRegistry.Types.Flag)
            {
                if (string.IsNullOrEmpty(_nodeData.GetProperty("TypeName", "")))
                    return true;
            }

            if (showFieldName && string.IsNullOrEmpty(_nodeData.GetProperty("FieldName", "")))
                return true;

            return false;
        }

        private void AddTextField(VisualElement c, string key, string label)
        {
            var field = new TextField(label) { value = _nodeData.GetProperty(key, "") };
            field.style.minWidth = 180;
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Property");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirtyDeferred();
            });
            c.Add(field);
        }

        private void AddColumnDropdown(VisualElement c, string key, string label)
        {
            var choices = _graphView.GraphAsset?.GetColumnChoices()
                ?? new List<string> { "A" };
            var cur = _nodeData.GetProperty(key, choices.Count > 0 ? choices[0] : "A");
            if (!choices.Contains(cur)) choices.Insert(0, cur);
            var field = new PopupField<string>(label, choices, cur);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Column");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirty();
            });
            c.Add(field);
        }

        private void AddSeparatorDropdown(VisualElement c, string key, string label)
        {
            var opts = new List<string>(NodeTypeRegistry.SeparatorOptions);
            var cur = _nodeData.GetProperty(key, ",");
            if (!opts.Contains(cur)) opts.Insert(0, cur);
            var field = new PopupField<string>(label, opts, cur);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Separator");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirty();
            });
            c.Add(field);
        }

        private void AddEnumDropdown(VisualElement c, string key, string label,
            List<string> opts)
        {
            var cur = _nodeData.GetProperty(key, opts[0]);
            if (!opts.Contains(cur)) opts.Insert(0, cur);
            var field = new PopupField<string>(label, opts, cur);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change Option");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirty();
            });
            c.Add(field);
        }

        private void AddGeneratedEnumDropdown(VisualElement c, string key,
            string label, bool isFlag)
        {
            var choices = ScanGeneratedEnums(isFlag);
            var cur = _nodeData.GetProperty(key, "");
            if (choices.Count == 0) choices.Add("(none generated)");
            if (!string.IsNullOrEmpty(cur) && !choices.Contains(cur))
                choices.Insert(0, cur);

            var selected = string.IsNullOrEmpty(cur) ? choices[0] : cur;
            var field = new PopupField<string>(label, choices, selected);

            if (choices.Count > 0 && choices[0] != "(none generated)")
                _nodeData.SetProperty(key, choices[0]);

            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "(none generated)") return;
                RecordUndo("Change Enum Type");
                _nodeData.SetProperty(key, evt.newValue);
                MarkDirty();
            });
            c.Add(field);
        }

        private static List<string> ScanGeneratedEnums(bool isFlag)
        {
            var result = new List<string>();
            var subfolder = isFlag ? "Flags" : "Enums";

            var searchPaths = new[]
            {
                $"{DataGraphSettings.Instance.Paths.GeneratedFolder}/{subfolder}",
                $"Assets/QuantumUser/Simulation/DataGraph/{subfolder}"
            };

            foreach (var sp in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(sp)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { sp }))
                {
                    var fn = System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (!string.IsNullOrEmpty(fn) && !result.Contains(fn))
                        result.Add(fn);
                }
            }

            return result;
        }

        private void RecordUndo(string msg)
        {
            if (_graphView.GraphAsset != null)
                Undo.RecordObject(_graphView.GraphAsset, msg);
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
