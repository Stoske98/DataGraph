using System.Collections.Generic;

namespace DataGraph.Editor
{
    /// <summary>
    /// Central registry of all DataGraph node types.
    /// v2: Unified structure nodes, no Root/Field split.
    /// </summary>
    internal static class NodeTypeRegistry
    {
        public static class Types
        {
            public const string Root = "Root";
            public const string Dictionary = "Dictionary";
            public const string VerticalArray = "VerticalArray";
            public const string HorizontalArray = "HorizontalArray";
            public const string Object = "Object";
            public const string Enum = "Enum";
            public const string Flag = "Flag";
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

        public static bool IsRoot(string t) => t == Types.Root;
        public static bool IsStructure(string t) => t is Types.Dictionary or Types.VerticalArray or Types.HorizontalArray or Types.Object;
        public static bool IsDefinition(string t) => t is Types.Enum or Types.Flag;
        public static bool IsLeaf(string t) => t is Types.StringField or Types.NumberField or Types.BoolField or Types.Vector2Field or Types.Vector3Field or Types.ColorField or Types.AssetField or Types.EnumField or Types.FlagField;
        public static bool HasChildrenPort(string t) => t is Types.Root or Types.Dictionary or Types.VerticalArray or Types.HorizontalArray or Types.Object;
        public static bool IsMultiChildPort(string t) => t == Types.Object;
        public static bool HasParentPort(string t) => t != Types.Root;
        public static bool ShouldShowFieldName(string parentType) => parentType == Types.Object;
        public static bool ShouldShowIndexColumn(string parentType) => parentType != null && parentType != Types.Root;

        public static string GetDisplayTitle(string t) => t switch
        {
            Types.Root => "Root", Types.Dictionary => "Dictionary", Types.VerticalArray => "Vertical Array",
            Types.HorizontalArray => "Horizontal Array", Types.Object => "Object",
            Types.Enum => "Enum", Types.Flag => "Flag",
            Types.StringField => "String", Types.NumberField => "Number", Types.BoolField => "Bool",
            Types.Vector2Field => "Vector2", Types.Vector3Field => "Vector3", Types.ColorField => "Color",
            Types.AssetField => "Asset", Types.EnumField => "Enum Field", Types.FlagField => "Flag Field",
            _ => t
        };

        public static List<(string category, string typeName, string displayName)> GetSearchEntries() => new()
        {
            ("Structures", Types.Dictionary, "Dictionary"),
            ("Structures", Types.VerticalArray, "Vertical Array"),
            ("Structures", Types.HorizontalArray, "Horizontal Array"),
            ("Structures", Types.Object, "Object"),
            ("Leaves", Types.StringField, "String"),
            ("Leaves", Types.NumberField, "Number"),
            ("Leaves", Types.BoolField, "Bool"),
            ("Leaves", Types.Vector2Field, "Vector2"),
            ("Leaves", Types.Vector3Field, "Vector3"),
            ("Leaves", Types.ColorField, "Color"),
            ("Leaves", Types.AssetField, "Asset"),
            ("Leaves", Types.EnumField, "Enum Field"),
            ("Leaves", Types.FlagField, "Flag Field"),
        };

        public static readonly string[] SeparatorOptions = { ",", ";", "|", ":", "/" };

        public static Dictionary<string, string> GetDefaultProperties(string t) => t switch
        {
            Types.Root => new(),
            Types.Dictionary => new() { ["KeyColumn"] = "A", ["KeyType"] = "Int" },
            Types.VerticalArray => new(),
            Types.HorizontalArray => new() { ["Separator"] = "," },
            Types.Object => new() { ["TypeName"] = "" },
            Types.Enum => new() { ["TypeName"] = "", ["NameColumn"] = "A", ["ValueColumn"] = "B" },
            Types.Flag => new() { ["TypeName"] = "", ["NameColumn"] = "A", ["ValueColumn"] = "B" },
            Types.StringField => new() { ["Column"] = "A" },
            Types.NumberField => new() { ["Column"] = "A", ["NumberType"] = "Int" },
            Types.BoolField => new() { ["Column"] = "A" },
            Types.Vector2Field => new() { ["Column"] = "A", ["Separator"] = "," },
            Types.Vector3Field => new() { ["Column"] = "A", ["Separator"] = "," },
            Types.ColorField => new() { ["Column"] = "A", ["Format"] = "Hex" },
            Types.AssetField => new() { ["Column"] = "A", ["AssetType"] = "Sprite" },
            Types.EnumField => new() { ["Column"] = "A", ["EnumTypeName"] = "" },
            Types.FlagField => new() { ["Column"] = "A", ["FlagTypeName"] = "", ["Separator"] = "," },
            _ => new()
        };

        public static UnityEngine.Color GetNodeColor(string t)
        {
            if (IsRoot(t)) return new UnityEngine.Color(0.15f, 0.3f, 0.55f);
            if (IsStructure(t)) return new UnityEngine.Color(0.5f, 0.3f, 0.7f);
            if (IsDefinition(t)) return new UnityEngine.Color(0.2f, 0.45f, 0.75f);
            return new UnityEngine.Color(0.2f, 0.6f, 0.35f);
        }
    }
}
