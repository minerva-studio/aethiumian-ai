using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Writes the internal DOM as deterministic, dependency-free YAML.</summary>
    internal static class DomYamlWriter
    {
        internal static string Write(DomValue value)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value ?? DomNull.Instance, 0);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, DomValue value, int indent)
        {
            switch (value)
            {
                case DomNull:
                    builder.Append("null");
                    return;
                case DomScalar scalar:
                    builder.Append(FormatScalar(scalar.Value));
                    return;
                case DomMapping mapping:
                    WriteMapping(builder, mapping, indent);
                    return;
                case DomSequence sequence:
                    WriteSequence(builder, sequence, indent);
                    return;
                default:
                    builder.Append("null");
                    return;
            }
        }

        private static void WriteMapping(StringBuilder builder, DomMapping mapping, int indent)
        {
            if (mapping.Properties.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            bool first = true;
            foreach (DomProperty property in mapping.Properties)
            {
                if (!first)
                {
                    builder.Append('\n');
                }

                WriteProperty(builder, property, indent, true);

                first = false;
            }
        }

        private static void WriteSequence(StringBuilder builder, DomSequence sequence, int indent)
        {
            if (sequence.Items.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            bool first = true;
            foreach (DomValue item in sequence.Items)
            {
                if (!first)
                {
                    builder.Append('\n');
                }

                AppendIndent(builder, indent);
                builder.Append('-');
                if (item is DomMapping mapping && mapping.Properties.Count > 0)
                {
                    builder.Append(' ');
                    WriteProperty(builder, mapping.Properties[0], indent + 2, false);
                    for (int propertyIndex = 1; propertyIndex < mapping.Properties.Count; propertyIndex++)
                    {
                        builder.Append('\n');
                        WriteProperty(builder, mapping.Properties[propertyIndex], indent + 2, true);
                    }
                }
                else if (IsScalarLike(item))
                {
                    builder.Append(' ');
                    WriteValue(builder, item, indent + 2);
                }
                else if (item is DomSequence nested && nested.Items.Count > 0)
                {
                    builder.Append('\n');
                    WriteSequence(builder, nested, indent + 2);
                }
                else
                {
                    builder.Append(" null");
                }

                first = false;
            }
        }

        private static void WriteProperty(StringBuilder builder, DomProperty property, int indent, bool includeIndent)
        {
            if (includeIndent)
            {
                AppendIndent(builder, indent);
            }

            builder.Append(FormatKey(property.Name));
            builder.Append(':');
            if (IsScalarLike(property.Value))
            {
                builder.Append(' ');
                WriteValue(builder, property.Value, indent + 2);
            }
            else if (property.Value is DomMapping childMapping && childMapping.Properties.Count == 0)
            {
                builder.Append(" {}");
            }
            else if (property.Value is DomSequence childSequence && childSequence.Items.Count == 0)
            {
                builder.Append(" []");
            }
            else
            {
                builder.Append('\n');
                WriteValue(builder, property.Value, indent + 2);
            }
        }

        private static bool IsScalarLike(DomValue value)
        {
            return value is DomNull || value is DomScalar;
        }

        private static string FormatKey(string key)
        {
            return IsPlainSafe(key) ? key : Quote(key);
        }

        private static string FormatScalar(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string text)
            {
                return IsPlainSafe(text) ? text : Quote(text);
            }

            if (value is bool boolean)
            {
                return boolean ? "true" : "false";
            }

            if (value is float single)
            {
                return single.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is double doubleValue)
            {
                return doubleValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is decimal decimalValue)
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value is Enum)
            {
                return value.ToString();
            }

            if (value is Vector2 vector2)
            {
                return "[" + FormatScalar(vector2.x) + ", " + FormatScalar(vector2.y) + "]";
            }

            if (value is Vector3 vector3)
            {
                return "[" + FormatScalar(vector3.x) + ", " + FormatScalar(vector3.y) + ", " + FormatScalar(vector3.z) + "]";
            }

            if (value is Vector4 vector4)
            {
                return "[" + FormatScalar(vector4.x) + ", " + FormatScalar(vector4.y) + ", " + FormatScalar(vector4.z) + ", " + FormatScalar(vector4.w) + "]";
            }

            if (value is Color color)
            {
                return "[" + FormatScalar(color.r) + ", " + FormatScalar(color.g) + ", " + FormatScalar(color.b) + ", " + FormatScalar(color.a) + "]";
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }

        private static bool IsPlainSafe(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value == "null" || value == "true" || value == "false" || value == "~"
                || value.Equals(".nan", StringComparison.OrdinalIgnoreCase)
                || value.Equals(".inf", StringComparison.OrdinalIgnoreCase)
                || value.Equals("-.inf", StringComparison.OrdinalIgnoreCase)) return false;
            if (value[0] == '-' || value[0] == '?' || value[0] == ':' || value[0] == '!' || value[0] == '&'
                || value[0] == '*' || value[0] == '#' || value[0] == '{' || value[0] == '}' || value[0] == '['
                || value[0] == ']' || value[0] == ',' || value[0] == '>' || value[0] == '|' || value[0] == '@'
                || value[0] == '`' || char.IsWhiteSpace(value[0])) return false;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) || character == ':' || character == '#' || character == '\n' || character == '\r')
                {
                    return false;
                }
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            return !char.IsWhiteSpace(value[value.Length - 1]);
        }

        private static string Quote(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(character); break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            builder.Append(' ', Math.Max(0, indent));
        }
    }
}
