using System;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Parses enum values from string cells. Handles both single-value and
    /// [Flags] enums by splitting on any of the supported separators
    /// (| ; ,), trimming each part, and combining for Enum.Parse.
    /// </summary>
    internal static class EnumParser
    {
        /// <summary>
        /// Parses <paramref name="raw"/> into the given enum type.
        /// Empty input returns the enum's default value.
        /// </summary>
        public static object Parse(Type enumType, string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return Activator.CreateInstance(enumType);

            var parts = raw.Split(new[] { '|', ';', ',' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            return Enum.Parse(enumType, string.Join(", ", parts), true);
        }
    }
}
