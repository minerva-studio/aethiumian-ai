using Amlos.AI.Nodes;
using Amlos.AI.References;
using System;
using UnityEngine;
using static Minerva.Module.VectorUtilities;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// Variable Utility class that handle variable casting in the system
    /// Author: Wendell Cai
    /// </summary>
    public static class VariableUtility
    {
        public static readonly VariableType[] UnityObjectAndGenerics = { VariableType.Generic, VariableType.UnityObject };
        public static readonly VariableType[] ALL = {
            VariableType.Int,
            VariableType.Float,
            VariableType.String,
            VariableType.Bool,
            VariableType.Vector2,
            VariableType.Vector3,
            VariableType.Vector4,
            VariableType.Generic,
            VariableType.UnityObject
        };

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
                    return ToVector2(value);
                case VariableType.Vector3:
                    return ToVector3(value);
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
        public static T ImplicitConversion<T>(object value)
        {
            var obj = ImplicitConversion(typeof(T), value);
            if (obj is T val)
            {
                return val;
            }
            return default;
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
            //null value case
            if (value is null)
            {
                return NullValueOf(type);
            }

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
                    {
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
                    }
                case VariableType.Vector3:
                    {
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
                    }
                case VariableType.Vector4:
                    {
                        if (value is Vector4)
                        {
                            return value;
                        }
                        if (value is Vector3 v3)
                        {
                            return (Vector4)v3;
                        }
                        else if (value is Vector3Int v3i)
                        {
                            return (Vector4)(Vector3)v3i;
                        }
                        else if (value is Vector2 v2)
                        {
                            return (Vector4)v2;
                        }
                        else if (value is Vector2Int v2i)
                        {
                            return (Vector4)(Vector2)v2i;
                        }
                        else if (value is bool b)
                        {
                            return b ? Vector4.one : Vector4.zero;
                        }
                        else if (value is UnityEngine.Object obj)
                        {
                            return obj ? Vector4.one : Vector4.zero;
                        }
                        else throw new InvalidCastException(value.ToString());
                    }
                case VariableType.UnityObject:
                    if (value is UnityEngine.Object)
                    {
                        return value;
                    }
                    else throw new InvalidCastException(value.ToString());
                case VariableType.Generic:
                    return value;
                default: throw new InvalidCastException(value.ToString());
            }
        }

        /// <summary>
        /// Implicit converstion between supported variables
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"> If variables cannot cast to each other, ie string -> bool </exception>
        public static object ImplicitConversion(Type restrictedType, object value)
        {
            if (value is null)
            {
                return NullValueOf(restrictedType);
            }

            //basic polymorphism
            Type type = value.GetType();
            if (type.IsSubclassOf(restrictedType) || type == restrictedType)
            {
                return value;
            }

            if (restrictedType == typeof(int)) return ImplicitConversion(VariableType.Int, value);
            else if (restrictedType.IsEnum)
            {
                return Enum.TryParse(restrictedType, ImplicitConversion(VariableType.Int, value).ToString(), out var e) ? e : 0;
            }
            else if (restrictedType == typeof(float)) return ImplicitConversion(VariableType.Float, value);
            else if (restrictedType == typeof(string)) return ImplicitConversion(VariableType.String, value);
            else if (restrictedType == typeof(bool)) return ImplicitConversion(VariableType.Bool, value);
            else if (restrictedType == typeof(Vector2) || restrictedType == typeof(Vector2Int)) return ImplicitConversion(VariableType.Vector2, value);
            else if (restrictedType == typeof(Vector3) || restrictedType == typeof(Vector3Int)) return ImplicitConversion(VariableType.Vector3, value);
            else if (restrictedType == typeof(Vector4)) return ImplicitConversion(VariableType.Vector4, value);
            else if (restrictedType == typeof(Color))
            {
                return (Color)(Vector4)ImplicitConversion(VariableType.Vector4, value);
            }
            else if (restrictedType == typeof(Rect))
            {
                var v4 = (Vector4)ImplicitConversion(VariableType.Vector4, value);
                return new Rect(v4.x, v4.y, v4.z, v4.w);
            }
            else if (restrictedType == typeof(RectInt))
            {
                var v4 = (Vector4)ImplicitConversion(VariableType.Vector4, value);
                return new RectInt((int)v4.x, (int)v4.y, (int)v4.z, (int)v4.w);
            }

            if (restrictedType.IsSubclassOf(typeof(Component)))
            {
                if (value is GameObject gameObject)
                {
                    return gameObject.GetComponent(restrictedType);
                }
            }
            else if (restrictedType == typeof(GameObject))
            {
                if (value is Component component)
                {
                    return component.gameObject;
                }
            }

            Debug.Log(value);
            Debug.Log(restrictedType);
            throw new InvalidCastException();
        }




        private static object NullValueOf(VariableType type)
        {
            return type switch
            {
                VariableType.String => string.Empty,
                VariableType.Int => 0,
                VariableType.Float => 0f,
                VariableType.Bool => false,
                VariableType.Vector2 => Vector2.zero,
                VariableType.Vector3 => Vector3.zero,
                VariableType.Vector4 => Vector4.zero,
                VariableType.UnityObject or VariableType.Generic => null,
                _ => throw new InvalidCastException(),
            };
        }

        private static object NullValueOf(Type restrictedType)
        {
            if (!restrictedType.IsValueType)
            {
                return null;
            }
            return NullValueOf(GetVariableType(restrictedType));
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
                    return Array(VariableType.String, VariableType.Float, VariableType.Bool, VariableType.Int, VariableType.Vector2, VariableType.Vector3, VariableType.Vector4, VariableType.UnityObject, VariableType.Generic);
                case VariableType.Int:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Float:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Bool:
                    return Array(VariableType.Bool, VariableType.Float, VariableType.Int, VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject);
                case VariableType.Vector2:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                case VariableType.Vector3:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                case VariableType.Vector4:
                    return Array(type);
                case VariableType.UnityObject:
                    return Array(type);
                case VariableType.Generic:
                    return (VariableType[])ALL.Clone();
                default:
                    return Array(type);
            }

            static VariableType[] Array(params VariableType[] variableTypes)
            {
                return variableTypes;
            }
        }

        public static VariableType GetVariableType<T>() => GetVariableType(typeof(T));
        public static VariableType GetVariableType(Type restrictedType)
        {
            if (restrictedType == typeof(int) || restrictedType.IsEnum) return VariableType.Int;
            if (restrictedType == typeof(uint)) return VariableType.Int;
            if (restrictedType == typeof(LayerMask)) return VariableType.Int;
            if (restrictedType == typeof(float)) return VariableType.Float;
            if (restrictedType == typeof(string)) return VariableType.String;
            if (restrictedType == typeof(bool)) return VariableType.Bool;
            if (restrictedType == typeof(Vector2) || restrictedType == typeof(Vector2Int)) return VariableType.Vector2;
            if (restrictedType == typeof(Vector3) || restrictedType == typeof(Vector3Int)) return VariableType.Vector3;
            if (restrictedType == typeof(Vector4) || restrictedType == typeof(Color)) return VariableType.Vector4;
            if (restrictedType == typeof(NodeProgress)) return VariableType.Node;
            if (restrictedType == typeof(UnityEngine.Object)) return VariableType.UnityObject;
            if (restrictedType.IsSubclassOf(typeof(UnityEngine.Object))) return VariableType.UnityObject;
            return VariableType.Generic;
        }






        public static Type GetType(VariableType variableType)
        {
            return variableType switch
            {
                VariableType.Node => typeof(NodeReference),
                VariableType.String => typeof(string),
                VariableType.Int => typeof(int),
                VariableType.Float => typeof(float),
                VariableType.Bool => typeof(bool),
                VariableType.Vector2 => typeof(Vector2),
                VariableType.Vector3 => typeof(Vector3),
                VariableType.Vector4 => typeof(Vector4),
                VariableType.UnityObject => typeof(UnityEngine.Object),
                VariableType.Generic => typeof(object),
                _ => null,
            };
        }

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
                case Vector4:
                    return VariableType.Vector4;
                case UnityEngine.Object:
                    return VariableType.UnityObject;
                default:
                    return VariableType.Generic;
            }
        }
    }
}
