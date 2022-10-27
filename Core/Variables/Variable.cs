using Amlos.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI
{

    [Serializable]
    public class VariableData
    {
        public UUID uuid;
        public VariableType type;
        public string name;
        public string defaultValue;

        public VariableData()
        {
            uuid = UUID.NewUUID();
        }
        public VariableData(string name, string defaultValue) : this()
        {
            this.name = name;
            this.defaultValue = defaultValue;
        }

        /// <summary>
        /// Check is the variable a valid variable that has its uuid label
        /// </summary>
        public bool isValid => uuid != UUID.Empty;
    }


    [Serializable]
    public class Variable
    {
        public UUID uuid;
        public VariableType type;
        public string name;
        public object value;

        /// <summary>
        /// the real value stored inside
        /// </summary>
        public object Value { get => value; }

        public string stringValue => GetValue<string>();
        public int intValue => GetValue<int>();
        public float floatValue => GetValue<float>();
        public bool boolValue => GetValue<bool>();
        public Vector2 vector2Value => GetValue<Vector2>();
        public Vector3 vector3Value => GetValue<Vector3>();



        public Variable(string name)
        {
            this.name = name;
            uuid = UUID.NewUUID();
        }
        public Variable(string name, object defaultValue)
        {
            this.name = name;
            this.value = defaultValue;
            uuid = UUID.NewUUID();
        }
        public Variable(VariableData data)
        {
            this.uuid = data.uuid;
            this.name = data.name;
            this.type = data.type;
            this.value = data.type.Parse(data.defaultValue);
        }

        public void SetValue(object value)
        {
            switch (type)
            {
                case VariableType.String:
                    if (value is string)
                    {
                        this.value = value;
                    }
                    else this.value = value.ToString();
                    break;
                case VariableType.Int:
                    if (value is int)
                    {
                        this.value = value;
                    }
                    else if (value is float f)
                    {
                        this.value = (int)f;
                    }
                    else if (value is bool b)
                    {
                        this.value = b ? 1 : 0;
                    }
                    else throw new InvalidCastException();
                    break;
                case VariableType.Float:
                    if (value is int i)
                    {
                        this.value = (float)i;
                    }
                    else if (value is float)
                    {
                        this.value = value;
                    }
                    else if (value is bool b)
                    {
                        this.value = b ? 1 : 0;
                    }
                    else throw new InvalidCastException();
                    break;
                case VariableType.Bool:
                    if (value is bool)
                    {
                        this.value = value;
                    }
                    else if (value is float f)
                    {
                        this.value = f != 0;
                    }
                    else if (value is int n)
                    {
                        this.value = n != 0;
                    }
                    else if (value is Vector2 vector2)
                    {
                        this.value = vector2 != Vector2.zero;
                    }
                    else if (value is Vector3 vector3)
                    {
                        this.value = vector3 != Vector3.zero;
                    }
                    else throw new InvalidCastException();
                    break;
                case VariableType.Vector2:
                    if (value is Vector2)
                    {
                        this.value = value;
                    }
                    else if (value is Vector3 v3)
                    {
                        this.value = (Vector2)v3;
                    }
                    else if (value is bool b)
                    {
                        this.value = b ? Vector2.one : Vector2.zero;
                    }
                    else throw new InvalidCastException();
                    break;
                case VariableType.Vector3:
                    if (value is Vector3)
                    {
                        this.value = value;
                    }
                    else if (value is Vector2 v2)
                    {
                        this.value = (Vector3)v2;
                    }
                    else if (value is bool b)
                    {
                        this.value = b ? Vector3.one : Vector3.zero;
                    }
                    else throw new InvalidCastException();
                    break;
                default:
                    break;
            }
        }

        protected T GetValue<T>()
        {
            var type = VariableTypeExtensions.GetVariableType(typeof(T));
            T t = (T)GetValue(type);
            return t;
        }

        protected object GetValue(VariableType type)
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
                    if (value is int i)
                    {
                        return (float)i;
                    }
                    else if (value is float)
                    {
                        return value;
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
    }

    public static class VariableExtensions
    {
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

        public static object Parse(this VariableType type, string value)
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
    }
}
