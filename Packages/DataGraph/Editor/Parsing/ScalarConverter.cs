using System;
using System.Globalization;
using UnityEngine;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Converts a managed value (already parsed from raw cell text by
    /// <see cref="ValueCoercer"/>) into the concrete target field type
    /// during reflection-based population. Logs and returns a default
    /// value if conversion fails so a single bad cell does not abort the
    /// entire parse run.
    /// </summary>
    internal static class ScalarConverter
    {
        public static object Convert(Type targetType, object value)
        {
            if (value == null) return targetType == typeof(string) ? "" : null;
            if (targetType.IsInstanceOfType(value)) return value;
            if (targetType == typeof(string)) return value.ToString();
            if (targetType == typeof(int)) return System.Convert.ToInt32(value);
            if (targetType == typeof(float)) return System.Convert.ToSingle(value);
            if (targetType == typeof(double)) return System.Convert.ToDouble(value);
            if (targetType == typeof(bool)) return System.Convert.ToBoolean(value);
            if (targetType.IsEnum) return EnumParser.Parse(targetType, value.ToString());

            try
            {
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"DataGraph: failed to convert value '{value}' (type {value.GetType().Name}) " +
                    $"to '{targetType.Name}': {ex.Message}. Falling back to default.");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }
    }
}
