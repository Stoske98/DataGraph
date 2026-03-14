using System;
using System.Globalization;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.Parsing
{
    /// <summary>
    /// Converts raw string cell values to typed C# objects
    /// based on the target FieldValueType. Returns Result to
    /// signal coercion failures without exceptions.
    /// </summary>
    internal static class ValueCoercer
    {
        /// <summary>
        /// Coerces a raw string value to the target type.
        /// </summary>
        public static Result<object> Coerce(string raw, FieldValueType targetType, ParseableCustomField field)
        {
            if (string.IsNullOrEmpty(raw))
                return DefaultValue(targetType);

            return targetType switch
            {
                FieldValueType.String => Result<object>.Success(raw),
                FieldValueType.Int => CoerceInt(raw),
                FieldValueType.Float => CoerceFloat(raw),
                FieldValueType.Bool => CoerceBool(raw),
                FieldValueType.Vector2 => CoerceVector2(raw, field.Separator ?? ","),
                FieldValueType.Vector3 => CoerceVector3(raw, field.Separator ?? ","),
                FieldValueType.Color => CoerceColor(raw, field.Format ?? "hex"),
                FieldValueType.Enum => CoerceEnum(raw, field.EnumType),
                _ => Result<object>.Failure($"Unsupported field value type: {targetType}")
            };
        }

        /// <summary>
        /// Returns the C# System.Type corresponding to a FieldValueType.
        /// </summary>
        public static Type GetSystemType(FieldValueType valueType, Type enumType = null)
        {
            return valueType switch
            {
                FieldValueType.String => typeof(string),
                FieldValueType.Int => typeof(int),
                FieldValueType.Float => typeof(float),
                FieldValueType.Bool => typeof(bool),
                FieldValueType.Vector2 => typeof(UnityEngine.Vector2),
                FieldValueType.Vector3 => typeof(UnityEngine.Vector3),
                FieldValueType.Color => typeof(UnityEngine.Color),
                FieldValueType.Enum => enumType ?? typeof(int),
                _ => typeof(object)
            };
        }

        private static Result<object> DefaultValue(FieldValueType type)
        {
            return type switch
            {
                FieldValueType.String => Result<object>.Success(string.Empty),
                FieldValueType.Int => Result<object>.Success(0),
                FieldValueType.Float => Result<object>.Success(0f),
                FieldValueType.Bool => Result<object>.Success(false),
                FieldValueType.Vector2 => Result<object>.Success(UnityEngine.Vector2.zero),
                FieldValueType.Vector3 => Result<object>.Success(UnityEngine.Vector3.zero),
                FieldValueType.Color => Result<object>.Success(UnityEngine.Color.white),
                FieldValueType.Enum => Result<object>.Success(0),
                _ => Result<object>.Success(null)
            };
        }

        private static Result<object> CoerceInt(string raw)
        {
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return Result<object>.Success(value);

            if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                return Result<object>.Success((int)floatVal);

            return Result<object>.Failure($"Cannot convert '{raw}' to int.");
        }

        private static Result<object> CoerceFloat(string raw)
        {
            if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return Result<object>.Success(value);
            return Result<object>.Failure($"Cannot convert '{raw}' to float.");
        }

        private static Result<object> CoerceBool(string raw)
        {
            var trimmed = raw.Trim().ToLowerInvariant();
            return trimmed switch
            {
                "true" or "1" or "yes" => Result<object>.Success(true),
                "false" or "0" or "no" or "" => Result<object>.Success(false),
                _ => Result<object>.Failure($"Cannot convert '{raw}' to bool.")
            };
        }

        private static Result<object> CoerceVector2(string raw, string separator)
        {
            var parts = raw.Split(new[] { separator }, StringSplitOptions.None);
            if (parts.Length != 2)
                return Result<object>.Failure($"Vector2 requires 2 components separated by '{separator}', got '{raw}'.");

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                return Result<object>.Failure($"Vector2 x component '{parts[0]}' is not a valid float.");
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return Result<object>.Failure($"Vector2 y component '{parts[1]}' is not a valid float.");

            return Result<object>.Success(new UnityEngine.Vector2(x, y));
        }

        private static Result<object> CoerceVector3(string raw, string separator)
        {
            var parts = raw.Split(new[] { separator }, StringSplitOptions.None);
            if (parts.Length != 3)
                return Result<object>.Failure($"Vector3 requires 3 components separated by '{separator}', got '{raw}'.");

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                return Result<object>.Failure($"Vector3 x component '{parts[0]}' is not a valid float.");
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return Result<object>.Failure($"Vector3 y component '{parts[1]}' is not a valid float.");
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                return Result<object>.Failure($"Vector3 z component '{parts[2]}' is not a valid float.");

            return Result<object>.Success(new UnityEngine.Vector3(x, y, z));
        }

        private static Result<object> CoerceColor(string raw, string format)
        {
            var trimmed = raw.Trim();

            if (format == "hex" || trimmed.StartsWith("#"))
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString(
                        trimmed.StartsWith("#") ? trimmed : "#" + trimmed,
                        out var color))
                    return Result<object>.Success(color);
                return Result<object>.Failure($"Cannot parse '{raw}' as hex color.");
            }

            if (format == "rgba")
            {
                var parts = trimmed.Split(',');
                if (parts.Length < 3 || parts.Length > 4)
                    return Result<object>.Failure($"RGBA color requires 3-4 components, got '{raw}'.");

                if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                    return Result<object>.Failure($"Color R component '{parts[0]}' is not valid.");
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
                    return Result<object>.Failure($"Color G component '{parts[1]}' is not valid.");
                if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                    return Result<object>.Failure($"Color B component '{parts[2]}' is not valid.");
                float a = 1f;
                if (parts.Length == 4 && !float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a))
                    return Result<object>.Failure($"Color A component '{parts[3]}' is not valid.");

                return Result<object>.Success(new UnityEngine.Color(r, g, b, a));
            }

            return Result<object>.Failure($"Unknown color format '{format}'.");
        }

        private static Result<object> CoerceEnum(string raw, Type enumType)
        {
            if (enumType == null)
                return Result<object>.Failure("Enum type not specified.");

            var trimmed = raw.Trim();

            if (Enum.TryParse(enumType, trimmed, ignoreCase: true, out var result))
                return Result<object>.Success(result);

            if (int.TryParse(trimmed, out int intVal) && Enum.IsDefined(enumType, intVal))
                return Result<object>.Success(Enum.ToObject(enumType, intVal));

            return Result<object>.Failure($"Cannot convert '{raw}' to enum {enumType.Name}.");
        }
    }
}
