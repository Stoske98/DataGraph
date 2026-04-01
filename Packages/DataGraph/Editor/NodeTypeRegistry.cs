using System.Collections.Generic;

namespace DataGraph.Editor
{
    /// <summary>
    /// Central registry of all DataGraph node types.
    /// Maps string type names to metadata for adapter, graph editor, and search window.
    /// </summary>
    internal static class NodeTypeRegistry
    {
        public static class Types
        {
            public const string DictionaryRoot = "DictionaryRoot";
            public const string ArrayRoot = "ArrayRoot";
            public const string ObjectRoot = "ObjectRoot";
            public const string EnumRoot = "EnumRoot";
            public const string FlagRoot = "FlagRoot";

            public const string ObjectField = "ObjectField";
            public const string VerticalArrayField = "VerticalArrayField";
            public const string HorizontalArrayField = "HorizontalArrayField";
            public const string DictionaryField = "DictionaryField";

            public const string StringField = "StringField";
            public const string NumberField = "NumberField";
            public const string BoolField = "BoolField";
            public const string Vector2Field = "Vector2Field";
            public const string Vector3Field = "Vector3Field";
            public const string ColorField = "ColorField";
            public const string AssetField = "AssetField";
            public const string EnumField = "EnumField";
            public const string FlagField = "FlagField";
        }

        public static bool IsRootNode(string typeName)
        {
            return typeName is Types.DictionaryRoot or Types.ArrayRoot or Types.ObjectRoot
                or Types.EnumRoot or Types.FlagRoot;
        }

        public static bool IsDefinitionRoot(string typeName)
        {
            return typeName is Types.EnumRoot or Types.FlagRoot;
        }

        public static bool IsLeafNode(string typeName)
        {
            return typeName is Types.StringField or Types.NumberField or Types.BoolField
                or Types.Vector2Field or Types.Vector3Field or Types.ColorField
                or Types.AssetField or Types.EnumField or Types.FlagField;
        }

        public static bool IsStructuralField(string typeName)
        {
            return typeName is Types.ObjectField or Types.VerticalArrayField
                or Types.HorizontalArrayField or Types.DictionaryField;
        }

        public static bool HasChildrenPort(string typeName)
        {
            return IsRootNode(typeName) && !IsDefinitionRoot(typeName)
                || IsStructuralField(typeName);
        }

        public static bool HasParentPort(string typeName)
        {
            return !IsRootNode(typeName);
        }

        /// <summary>
        /// Returns the fixed display title for a node type.
        /// Root nodes show their category name. Structures and Leaves show short names.
        /// </summary>
        public static string GetDisplayTitle(string typeName)
        {
            return typeName switch
            {
                Types.DictionaryRoot => "Dictionary Root",
                Types.ArrayRoot => "Array Root",
                Types.ObjectRoot => "Object Root",
                Types.EnumRoot => "Enum Root",
                Types.FlagRoot => "Flag Root",

                Types.ObjectField => "Object",
                Types.VerticalArrayField => "Vertical Array",
                Types.HorizontalArrayField => "Horizontal Array",
                Types.DictionaryField => "Dictionary",

                Types.StringField => "String",
                Types.NumberField => "Number",
                Types.BoolField => "Bool",
                Types.Vector2Field => "Vector2",
                Types.Vector3Field => "Vector3",
                Types.ColorField => "Color",
                Types.AssetField => "Asset",
                Types.EnumField => "Enum",
                Types.FlagField => "Flag",

                _ => typeName
            };
        }

        /// <summary>
        /// Returns categorized entries for the search window.
        /// Categories: Roots, Structures, Leaves.
        /// </summary>
        public static List<(string category, string typeName, string displayName)> GetSearchEntries()
        {
            return new List<(string, string, string)>
            {
                ("Roots", Types.DictionaryRoot, "Dictionary Root"),
                ("Roots", Types.ArrayRoot, "Array Root"),
                ("Roots", Types.ObjectRoot, "Object Root"),
                ("Roots", Types.EnumRoot, "Enum Root"),
                ("Roots", Types.FlagRoot, "Flag Root"),

                ("Structures", Types.ObjectField, "Object"),
                ("Structures", Types.VerticalArrayField, "Vertical Array"),
                ("Structures", Types.HorizontalArrayField, "Horizontal Array"),
                ("Structures", Types.DictionaryField, "Dictionary"),

                ("Leaves", Types.StringField, "String"),
                ("Leaves", Types.NumberField, "Number"),
                ("Leaves", Types.BoolField, "Bool"),
                ("Leaves", Types.Vector2Field, "Vector2"),
                ("Leaves", Types.Vector3Field, "Vector3"),
                ("Leaves", Types.ColorField, "Color"),
                ("Leaves", Types.AssetField, "Asset"),
                ("Leaves", Types.EnumField, "Enum"),
                ("Leaves", Types.FlagField, "Flag"),
            };
        }

        public static readonly string[] SeparatorOptions = { ",", ";", "|", ":", "/" };

        /// <summary>
        /// Returns default properties for a given node type.
        /// TypeName and FieldName are empty by default — user must fill them in.
        /// </summary>
        public static Dictionary<string, string> GetDefaultProperties(string typeName)
        {
            return typeName switch
            {
                Types.DictionaryRoot => new() { ["KeyColumn"] = "A", ["KeyType"] = "Int" },
                Types.ArrayRoot => new(),
                Types.ObjectRoot => new(),
                Types.EnumRoot => new() { ["NameColumn"] = "A", ["ValueColumn"] = "B" },
                Types.FlagRoot => new() { ["NameColumn"] = "A", ["ValueColumn"] = "B" },

                Types.ObjectField => new() { ["TypeName"] = "", ["FieldName"] = "" },
                Types.VerticalArrayField => new() { ["TypeName"] = "", ["FieldName"] = "", ["IndexColumn"] = "A" },
                Types.HorizontalArrayField => new() { ["FieldName"] = "", ["Separator"] = "," },
                Types.DictionaryField => new() { ["TypeName"] = "", ["FieldName"] = "", ["KeyColumn"] = "A", ["KeyType"] = "Int" },

                Types.StringField => new() { ["FieldName"] = "", ["Column"] = "A" },
                Types.NumberField => new() { ["FieldName"] = "", ["Column"] = "A", ["NumberType"] = "Int" },
                Types.BoolField => new() { ["FieldName"] = "", ["Column"] = "A" },
                Types.Vector2Field => new() { ["FieldName"] = "", ["Column"] = "A", ["Separator"] = "," },
                Types.Vector3Field => new() { ["FieldName"] = "", ["Column"] = "A", ["Separator"] = "," },
                Types.ColorField => new() { ["FieldName"] = "", ["Column"] = "A", ["Format"] = "Hex" },
                Types.AssetField => new() { ["FieldName"] = "", ["Column"] = "A", ["AssetType"] = "Sprite" },
                Types.EnumField => new() { ["FieldName"] = "", ["Column"] = "A", ["EnumTypeName"] = "" },
                Types.FlagField => new() { ["FieldName"] = "", ["Column"] = "A", ["FlagTypeName"] = "", ["Separator"] = "," },

                _ => new()
            };
        }

        /// <summary>
        /// Returns a display color for node header by category.
        /// Roots: blue, Structures: purple, Leaves: green.
        /// </summary>
        public static UnityEngine.Color GetNodeColor(string typeName)
        {
            if (IsRootNode(typeName)) return new UnityEngine.Color(0.2f, 0.45f, 0.75f);
            if (IsStructuralField(typeName)) return new UnityEngine.Color(0.5f, 0.3f, 0.7f);
            return new UnityEngine.Color(0.2f, 0.6f, 0.35f);
        }
    }
}
