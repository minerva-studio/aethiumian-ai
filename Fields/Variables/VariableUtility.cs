using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Variable Utility class that handel variable casting in the system
    /// Author: Wendell Cai
    /// </summary>
    public static class VariableUtility
    {
        /// <summary>
        /// Get the variable type by an instance
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static VariableType GetType(object value)
        {
            switch (value)
            {
                case int:
                    return VariableType.Int;

                case string:
                    return VariableType.String;

                case float:
                    return VariableType.Float;

                case bool:
                    return VariableType.Bool;

                case Vector2:
                    return VariableType.Vector2;

                case Vector3:
                    return VariableType.Vector3;

            }
            return default;
        }

        /// <summary>
        /// Parse a string to given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object Parse(VariableType type, string value)
        {
            switch (type)
            {
                case VariableType.String:
                    return value.Clone();
                case VariableType.Int:
                    return int.Parse(value);
                case VariableType.Float:
                    return float.Parse(value);
                case VariableType.Bool:
                    return bool.Parse(value);
                case VariableType.Vector2:
                    return VectorUtilities.ToVector2(value);
                case VariableType.Vector3:
                    return VectorUtilities.ToVector3(value);
                default:
                    break;
            }
            return null;
        }

        /// <summary>
        /// Try parse a string to given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryParse(this VariableType type, string value, out object ret)
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
                    try
                    {
                        ret = VectorUtilities.ToVector2(value);
                        result = true;
                    }
                    catch
                    {
                        ret = Vector2.zero;
                        result = false;
                    }
                    break;
                case VariableType.Vector3:
                    try
                    {
                        ret = VectorUtilities.ToVector3(value);
                        result = true;
                    }
                    catch
                    {
                        ret = Vector3.zero;
                        result = false;
                    }
                    break;
                default:
                    ret = null;
                    break;
            }
            return result;
        }

        /// <summary>
        /// Implicit converstion between supported variables
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"> If variables cannot cast to each other, ie string -> bool </exception>
        public static object ImplicitConversion(VariableType type, object value)
        {
            switch (type)
            {
                case VariableType.String:
                    return value.ToString();
                case VariableType.Int:
                    if (value is int)
                    {
                        return value;
                    }
                    else if (value is float f)
                    {
                        return (int)f;
                    }
                    else if (value is bool b)
                    {
                        return b ? 1 : 0;
                    }
                    else if (value is UnityEngine.Object obj)
                    {
                        return obj ? 1 : 0;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Float:
                    if (value is float)
                    {
                        return value;
                    }
                    else if (value is int i)
                    {
                        return (float)i;
                    }
                    else if (value is bool b)
                    {
                        return b ? 1 : 0;
                    }
                    else if (value is UnityEngine.Object obj)
                    {
                        return obj ? 1 : 0;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Bool:
                    if (value is bool)
                    {
                        return value;
                    }
                    else if (value is float f)
                    {
                        return f != 0;
                    }
                    else if (value is int n)
                    {
                        return n != 0;
                    }
                    else if (value is Vector2 vector2)
                    {
                        return vector2 != Vector2.zero;
                    }
                    else if (value is Vector3 vector3)
                    {
                        return vector3 != Vector3.zero;
                    }
                    else if (value is UnityEngine.Object obj)
                    {
                        return (bool)obj;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Vector2:
                    if (value is Vector2)
                    {
                        return value;
                    }
                    else if (value is Vector2Int v2i)
                    {
                        return (Vector2)v2i;
                    }
                    else if (value is Vector3 v3)
                    {
                        return (Vector2)v3;
                    }
                    else if (value is Vector3Int v3i)
                    {
                        return (Vector2)(Vector3)v3i;
                    }
                    else if (value is bool b)
                    {
                        return b ? Vector2.one : Vector2.zero;
                    }
                    else if (value is UnityEngine.Object obj)
                    {
                        return obj ? Vector2.one : Vector2.zero;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Vector3:
                    if (value is Vector3)
                    {
                        return value;
                    }
                    else if (value is Vector3Int v3i)
                    {
                        return (Vector3)v3i;
                    }
                    else if (value is Vector2 v2)
                    {
                        return (Vector3)v2;
                    }
                    else if (value is Vector2Int v2i)
                    {
                        return (Vector3)(Vector2)v2i;
                    }
                    else if (value is bool b)
                    {
                        return b ? Vector3.one : Vector3.zero;
                    }
                    else if (value is UnityEngine.Object obj)
                    {
                        return obj ? Vector3.one : Vector3.zero;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.UnityObject:
                    if (value is UnityEngine.Object)
                    {
                        return value;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Generic:
                    return value;
                case VariableType.Vector4:
                default: throw new InvalidCastException(value.ToString());
            }
        }


        public static VariableType GetVariableType<T>()
        {
            T a = default;
            switch (a)
            {
                case Enum:
                case int:
                    return VariableType.Int;
                case string:
                    return VariableType.String;
                case bool:
                    return VariableType.Bool;
                case float:
                    return VariableType.Float;
                case Vector2:
                case Vector2Int:
                    return VariableType.Vector2;
                case Vector3:
                case Vector3Int:
                    return VariableType.Vector3;
                case Vector4:
                    return VariableType.Vector4;
                case UnityEngine.Object:
                    return VariableType.UnityObject;
                default:
                    break;
            }
            return default;
        }



        public static VariableType[] GetCompatibleTypes(Type type)
        {
            return GetCompatibleTypes(GetVariableType(type));
        }

        public static VariableType[] GetCompatibleTypes(VariableType type)
        {
            switch (type)
            {
                case VariableType.Node:
                    return Array(VariableType.Node);
                case VariableType.Invalid:
                    return Array();
                case VariableType.String:
                    return Array(VariableType.String, VariableType.Float, VariableType.Bool, VariableType.Int, VariableType.Vector2, VariableType.Vector3);
                case VariableType.Int:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Float:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Bool:
                    return Array(VariableType.Bool, VariableType.Float, VariableType.Int, VariableType.String, VariableType.Vector2, VariableType.Vector3);
                case VariableType.Vector2:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                case VariableType.Vector3:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                default:
                    return Array(type);
            }

            static VariableType[] Array(params VariableType[] variableTypes)
            {
                return variableTypes;
            }
        }

        public static VariableType GetVariableType(this Type type)
        {
            if (type == typeof(int) || type.IsEnum)
            {
                return VariableType.Int;
            }
            if (type == typeof(float))
            {
                return VariableType.Float;
            }
            if (type == typeof(string))
            {
                return VariableType.String;
            }
            if (type == typeof(bool))
            {
                return VariableType.Bool;
            }
            if (type == typeof(Vector2) || type == typeof(Vector2Int))
            {
                return VariableType.Vector2;
            }
            if (type == typeof(Vector3) || type == typeof(Vector3Int))
            {
                return VariableType.Vector3;
            }
            if (type == typeof(NodeProgress))
            {
                return VariableType.Node;
            }
            if (type.IsSubclassOf(typeof(UnityEngine.Object)) || type == typeof(UnityEngine.Object))
            {
                return VariableType.UnityObject;
            }

            return VariableType.Generic;
        }
    }
}
