using System;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    internal static class VectorUtility
    {
        /// <summary>
        /// Try parse a string to given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryParse(VariableType type, string value, out object ret)
        {
            var result = false;
            switch (type)
            {
                case VariableType.String:
                    ret = value;
                    break;
                case VariableType.Int:
                    result = int.TryParse(value, out int i);
                    ret = i;
                    break;
                case VariableType.Float:
                    result = float.TryParse(value, out float f);
                    ret = f;
                    break;
                case VariableType.Bool:
                    result = bool.TryParse(value, out bool b);
                    ret = b;
                    break;
                case VariableType.Vector2:
                    result = TryParseVector2(value, out Vector2 v2);
                    ret = v2;
                    break;
                case VariableType.Vector3:
                    result = TryParseVector3(value, out Vector3 v3);
                    ret = v3;
                    break;
                case VariableType.Vector4:
                    result = TryParseVector4(value, out Vector4 v4);
                    ret = v4;
                    break;
                default:
                    ret = null;
                    break;
            }
            return result;
        }

        /// <summary>
        /// Try parse a string into a Vector2 without allocating parser scratch data.
        /// </summary>
        public static bool TryParseVector2(string value, out Vector2 result)
        {
            Span<float> components = stackalloc float[2];
            if (TryParseFloatVector(value, nameof(Vector2), components))
            {
                result = new Vector2(components[0], components[1]);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Try parse a string into a Vector3 without allocating parser scratch data.
        /// </summary>
        public static bool TryParseVector3(string value, out Vector3 result)
        {
            Span<float> components = stackalloc float[3];
            if (TryParseFloatVector(value, nameof(Vector3), components))
            {
                result = new Vector3(components[0], components[1], components[2]);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Try parse a string into a Vector4 without allocating parser scratch data.
        /// </summary>
        public static bool TryParseVector4(string value, out Vector4 result)
        {
            Span<float> components = stackalloc float[4];
            if (TryParseFloatVector(value, nameof(Vector4), components))
            {
                result = new Vector4(components[0], components[1], components[2], components[3]);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Try parse a string into a Vector2Int without allocating parser scratch data.
        /// </summary>
        private static bool TryParseVector2Int(string value, out Vector2Int result)
        {
            Span<int> components = stackalloc int[2];
            if (TryParseIntegerVector(value, nameof(Vector2Int), components))
            {
                result = new Vector2Int(components[0], components[1]);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Try parse a string into a Vector3Int without allocating parser scratch data.
        /// </summary>
        private static bool TryParseVector3Int(string value, out Vector3Int result)
        {
            Span<int> components = stackalloc int[3];
            if (TryParseIntegerVector(value, nameof(Vector3Int), components))
            {
                result = new Vector3Int(components[0], components[1], components[2]);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Try parse fixed-length float vector components from a supported vector literal.
        /// </summary>
        private static bool TryParseFloatVector(string value, string typeName, Span<float> components)
        {
            if (!TryReadVectorBody(value.AsSpan(), typeName.AsSpan(), out ReadOnlySpan<char> body))
            {
                return false;
            }

            return TryParseVectorBody(body, components);
        }

        /// <summary>
        /// Try parse fixed-length integer vector components from a supported vector literal.
        /// </summary>
        private static bool TryParseIntegerVector(string value, string typeName, Span<int> components)
        {
            if (!TryReadVectorBody(value.AsSpan(), typeName.AsSpan(), out ReadOnlySpan<char> body))
            {
                return false;
            }

            return TryParseVectorBody(body, components);
        }

        /// <summary>
        /// Read the component body from either "x,y" / "(x,y)" or "VectorN(x,y)" syntax.
        /// </summary>
        private static bool TryReadVectorBody(ReadOnlySpan<char> value, ReadOnlySpan<char> typeName, out ReadOnlySpan<char> body)
        {
            ReadOnlySpan<char> span = value.Trim();
            if (span.Length == 0)
            {
                body = default;
                return false;
            }

            if (StartsWithOrdinalIgnoreCase(span, typeName))
            {
                span = span[typeName.Length..].TrimStart();
                if (span.Length < 2 || span[0] != '(' || span[^1] != ')')
                {
                    body = default;
                    return false;
                }

                body = span[1..^1];
                return true;
            }

            if (span[0] == '(' || span[^1] == ')')
            {
                if (span.Length < 2 || span[0] != '(' || span[^1] != ')')
                {
                    body = default;
                    return false;
                }

                body = span[1..^1];
                return true;
            }

            body = span;
            return true;
        }

        /// <summary>
        /// Parse comma-separated float components in a single pass through the body span.
        /// </summary>
        private static bool TryParseVectorBody(ReadOnlySpan<char> body, Span<float> components)
        {
            int componentIndex = 0;
            int tokenStart = 0;

            for (int i = 0; i <= body.Length; i++)
            {
                if (i < body.Length && body[i] != ',')
                {
                    continue;
                }

                if (componentIndex >= components.Length)
                {
                    return false;
                }

                ReadOnlySpan<char> token = body[tokenStart..i].Trim();
                if (token.Length == 0 || !float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out components[componentIndex]))
                {
                    return false;
                }

                componentIndex++;
                tokenStart = i + 1;
            }

            return componentIndex == components.Length;
        }

        /// <summary>
        /// Parse comma-separated integer components in a single pass through the body span.
        /// </summary>
        private static bool TryParseVectorBody(ReadOnlySpan<char> body, Span<int> components)
        {
            int componentIndex = 0;
            int tokenStart = 0;

            for (int i = 0; i <= body.Length; i++)
            {
                if (i < body.Length && body[i] != ',')
                {
                    continue;
                }

                if (componentIndex >= components.Length)
                {
                    return false;
                }

                ReadOnlySpan<char> token = body[tokenStart..i].Trim();
                if (token.Length == 0 || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out components[componentIndex]))
                {
                    return false;
                }

                componentIndex++;
                tokenStart = i + 1;
            }

            return componentIndex == components.Length;
        }

        /// <summary>
        /// Compare literal prefixes without allocating a normalized string.
        /// </summary>
        private static bool StartsWithOrdinalIgnoreCase(ReadOnlySpan<char> span, ReadOnlySpan<char> prefix)
        {
            if (span.Length < prefix.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (char.ToUpperInvariant(span[i]) != char.ToUpperInvariant(prefix[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
