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
                    else throw new InvalidCastException();
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
                    else throw new InvalidCastException();
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
                    else throw new InvalidCastException();
                case VariableType.Vector2:
                    if (value is Vector2)
                    {
                        return value;
                    }
                    else if (value is Vector3 v3)
                    {
                        return (Vector2)v3;
                    }
                    else if (value is bool b)
                    {
                        return b ? Vector2.one : Vector2.zero;
                    }
                    else throw new InvalidCastException();
                case VariableType.Vector3:
                    if (value is Vector3)
                    {
                        return value;
                    }
                    else if (value is Vector2 v2)
                    {
                        return (Vector3)v2;
                    }
                    else if (value is bool b)
                    {
                        return b ? Vector3.one : Vector3.zero;
                    }
                    else throw new InvalidCastException();
                default: throw new InvalidCastException();
            }
        }


        public static VariableType GetVariableType<T>()
        {
            T a = default;
            switch (a)
            {
                case int:
                    return VariableType.Int;
                case string:
                    return VariableType.String;
                case bool:
                    return VariableType.Bool;
                case float:
                    return VariableType.Float;
                case Vector2:
                    return VariableType.Vector2;
                case Vector3:
                    return VariableType.Vector3;
                default:
                    break;
            }
            return default;


            //if (typeof(T) == typeof(int))
            //{
            //    return VariableType.Int;
            //}
            //if (typeof(T) == typeof(string))
            //{
            //    return VariableType.String;
            //}
            //if (typeof(T) == typeof(bool))
            //{
            //    return VariableType.Bool;
            //}
            //if (typeof(T) == typeof(float))
            //{
            //    return VariableType.Float;
            //}
            //if (typeof(T) == typeof(Vector2))
            //{
            //    return VariableType.Vector2;
            //}
            //if (typeof(T) == typeof(Vector3))
            //{
            //    return VariableType.Vector3;
            //}
            //return default;
        }
    }
}
