using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Writes the internal DOM as deterministic compact JSON without adding package dependencies.</summary>
    internal static class DomJsonWriter
    {
        internal static string Write(DomValue value)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value ?? DomNull.Instance);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, DomValue value)
        {
            switch (value)
            {
                case DomNull:
                    builder.Append("null");
                    return;
                case DomScalar scalar:
                    WriteScalar(builder, scalar.Value);
                    return;
                case DomMapping mapping:
                    WriteMapping(builder, mapping);
                    return;
                case DomSequence sequence:
                    WriteSequence(builder, sequence);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported DOM value type: {value.GetType().FullName}.");
            }
        }

        private static void WriteMapping(StringBuilder builder, DomMapping mapping)
        {
            builder.Append('{');
            for (int index = 0; index < mapping.Properties.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                DomProperty property = mapping.Properties[index];
                WriteString(builder, property.Name);
                builder.Append(':');
                WriteValue(builder, property.Value);
            }

            builder.Append('}');
        }

        private static void WriteSequence(StringBuilder builder, DomSequence sequence)
        {
            builder.Append('[');
            for (int index = 0; index < sequence.Items.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                WriteValue(builder, sequence.Items[index]);
            }

            builder.Append(']');
        }

        private static void WriteScalar(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string || value is char || value is Enum)
            {
                WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }

            if (value is float single)
            {
                EnsureFinite(single);
                builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is double doubleValue)
            {
                EnsureFinite(doubleValue);
                builder.Append(doubleValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is Vector2 vector2)
            {
                WriteFloatArray(builder, vector2.x, vector2.y);
                return;
            }

            if (value is Vector3 vector3)
            {
                WriteFloatArray(builder, vector3.x, vector3.y, vector3.z);
                return;
            }

            if (value is Vector4 vector4)
            {
                WriteFloatArray(builder, vector4.x, vector4.y, vector4.z, vector4.w);
                return;
            }

            if (value is Color color)
            {
                WriteFloatArray(builder, color.r, color.g, color.b, color.a);
                return;
            }

            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteFloatArray(StringBuilder builder, params float[] values)
        {
            builder.Append('[');
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                EnsureFinite(values[index]);
                builder.Append(values[index].ToString("R", CultureInfo.InvariantCulture));
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static void EnsureFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException("JSON DOM export cannot represent a non-finite floating-point value.");
            }
        }

        private static void EnsureFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException("JSON DOM export cannot represent a non-finite floating-point value.");
            }
        }
    }
}
