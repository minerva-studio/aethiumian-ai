using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using static Minerva.Module.VectorUtility;

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
                    return int.Parse(value, provider: CultureInfo.InvariantCulture);
                case VariableType.Float:
                    return float.Parse(value, provider: CultureInfo.InvariantCulture);
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
        /// Creates a stable UUID based on the given string.
        /// Same input string will always return the same UUID.
        /// </summary>
        public static UUID CreateStableUUID(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            // Use SHA1 (20 bytes) and take the first 16 bytes
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));

            byte[] guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);

            return new UUID(new Guid(guidBytes));
        }


        /// <summary>
        /// Create the variable by given a data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="target"></param>
        /// <param name="isGlobal"></param>
        /// <returns></returns>
        public static Variable Create(VariableData data, object target, bool isGlobal = false)
        {
            if (data.IsScript)
            {
                return new TargetScriptVariable(data, target);
            }
            return new TreeVariable(data, isGlobal);
        }







        /// <summary>
        /// Implicit converstion between supported variables
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"> If variables cannot cast to each other, ie string -> bool </exception>
        public static object ImplicitConversion<T>(VariableType type, T value)
        {
            switch (type)
            {
                case VariableType.String:
                    return ImplicitConversion<string, T>(value);
                case VariableType.Int:
                    return ImplicitConversion<int, T>(value);
                case VariableType.Float:
                    return ImplicitConversion<float, T>(value);
                case VariableType.Bool:
                    return ImplicitConversion<bool, T>(value);
                case VariableType.Vector2:
                    return ImplicitConversion<Vector2, T>(value);
                case VariableType.Vector3:
                    return ImplicitConversion<Vector3, T>(value);
                case VariableType.Vector4:
                    return ImplicitConversion<Vector4, T>(value);
                case VariableType.UnityObject:
                    return ImplicitConversion<UnityEngine.Object, T>(value);
                case VariableType.Generic:
                    return value;
                default:
                case VariableType.Node:
                case VariableType.Invalid:
                    break;
            }
            throw new InvalidCastException();
        }

        /// <summary>
        /// Implicit converstion between supported variables
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"> If variables cannot cast to each other, ie string -> bool </exception>
        public static T ImplicitConversion<T>(object value) => ImplicitConversion<T, object>(value);

        public static TResult ImplicitConversion<TResult, TValue>(TValue value)
        {
            if (value is TResult polymorphicResult) return polymorphicResult;

            if (Converter.Default is IConverter<TResult> converter)
            {
                return converter.Convert(value);
            }
            if (Converter.Default is IContravariantConverter<TResult> contravariantConverter)
            {
                return (TResult)contravariantConverter.Convert<TResult, TValue>(value);
            }

            Type type = typeof(TResult);
            if (type.IsEnum)
            {
                return Converter.Default.ConvertTo<TResult, TValue>(value);
            }
            // not a value type and is null
            if (!type.IsValueType && value is null)
            {
                return default;
            }
#if UNITY_WEBGL
            // for some reason, webassembly cannot do contravariant well, have to determine the following by explicitly calling types
            // gameObject casting to component
            if (value is GameObject go && go.TryGetComponent<TResult>(out var r))
            {
                return r;
            }
            // the other way
            if (value is Component c)
                if (typeof(TResult) == typeof(GameObject))
                {
                    return (TResult)(object)c.gameObject;
                }
                else if (typeof(Component).IsAssignableFrom(typeof(TResult)) && c.TryGetComponent(out r))
                {
                    return r;
                }
#endif


            Debug.Log($"{type}: {value?.ToString() ?? "null"}");
            throw InvalidCast<TResult>(value);
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
            else if (restrictedType == typeof(float)) return ImplicitConversion(VariableType.Float, value);
            else if (restrictedType == typeof(string)) return ImplicitConversion(VariableType.String, value);
            else if (restrictedType == typeof(bool)) return ImplicitConversion(VariableType.Bool, value);
            else if (restrictedType == typeof(Vector2) || restrictedType == typeof(Vector2Int)) return ImplicitConversion(VariableType.Vector2, value);
            else if (restrictedType == typeof(Vector3) || restrictedType == typeof(Vector3Int)) return ImplicitConversion(VariableType.Vector3, value);
            else if (restrictedType == typeof(Vector4)) return ImplicitConversion(VariableType.Vector4, value);
            else if (restrictedType == typeof(Color)) return (Color)(Vector4)ImplicitConversion(VariableType.Vector4, value);
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
            else if (restrictedType.IsEnum) return Enum.TryParse(restrictedType, ImplicitConversion(VariableType.Int, value).ToString(), out var e) ? e : 0;


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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableType GetVariableType<T>() => VariableTypeProvider<T>.Type;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (restrictedType == typeof(CancellationToken)) return VariableType.Node;
            if (restrictedType == typeof(UnityEngine.Object)) return VariableType.UnityObject;
            if (restrictedType.IsSubclassOf(typeof(UnityEngine.Object))) return VariableType.UnityObject;
            return VariableType.Generic;
        }

        public static VariableType? GetVariableType(VariableData vd, Type targetClass = null)
        {
            try
            {
                if (!vd.IsScript)
                {
                    return vd.Type;
                }
                if (targetClass != null)
                {
                    MemberInfo memberInfo = targetClass.GetMember(vd.Path)[0];
                    var memberResultType = GetResultType(memberInfo);
                    return GetVariableType(memberResultType);
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }






        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableType GetType(object value)
        {
            return value switch
            {
                int => VariableType.Int,
                string => VariableType.String,
                float => VariableType.Float,
                bool => VariableType.Bool,
                Vector2 => VariableType.Vector2,
                Vector3 => VariableType.Vector3,
                Vector4 => VariableType.Vector4,
                UnityEngine.Object => VariableType.UnityObject,
                _ => VariableType.Generic,
            };
        }





        public static Type GetResultType(MemberInfo member)
        {
            return member switch
            {
                FieldInfo f => f.FieldType,
                PropertyInfo p => p.PropertyType,
                MethodInfo methodInfo => methodInfo.ReturnType,
                _ => null,
            };
        }

        public static bool IsStatic(MemberInfo member)
        {
            FieldInfo fieldInfo = member as FieldInfo;
            if (fieldInfo != null)
            {
                return fieldInfo.IsStatic;
            }

            PropertyInfo propertyInfo = member as PropertyInfo;
            if (propertyInfo != null)
            {
                if (!propertyInfo.CanRead)
                {
                    return propertyInfo.GetSetMethod(nonPublic: true).IsStatic;
                }

                return propertyInfo.GetGetMethod(nonPublic: true).IsStatic;
            }

            MethodBase methodBase = member as MethodBase;
            if (methodBase != null)
            {
                return methodBase.IsStatic;
            }

            EventInfo eventInfo = member as EventInfo;
            if (eventInfo != null)
            {
                return eventInfo.GetRaiseMethod(nonPublic: true).IsStatic;
            }

            Type type = member as Type;
            if (type != null)
            {
                if (type.IsSealed)
                {
                    return type.IsAbstract;
                }

                return false;
            }

            string message = string.Format(CultureInfo.InvariantCulture, "Unable to determine IsStatic for member {0}.{1}MemberType was {2} but only fields, properties, methods, events and types are supported.", member.DeclaringType.FullName, member.Name, member.GetType().FullName);
            throw new NotSupportedException(message);
        }

        public static bool CanRead(MemberInfo memberInfo)
        {
            return (memberInfo is MethodInfo m && m.ReturnType == typeof(void))
                || (memberInfo is PropertyInfo p && p.CanRead)
                || memberInfo is FieldInfo;
        }

        public static bool CanWrite(MemberInfo memberInfo)
        {
            return (memberInfo is MethodInfo m1 && m1.GetParameters().Length == 0)
                || (memberInfo is PropertyInfo p2 && p2.CanWrite)
                || memberInfo is FieldInfo;
        }





        interface IContravariantConverter<in TTarget>
        {
            object Convert<TTargetValue, T>(T target) where TTargetValue : TTarget;
        }

        interface IConverter<TTarget>
        {
            TTarget Convert<T>(T value);
        }

        struct Converter :
            IConverter<string>,
            IConverter<int>,
            IConverter<bool>,
            IConverter<float>,
            IConverter<Vector2>,
            IConverter<Vector3>,
            IConverter<Vector4>,
            IConverter<Color>,
            IConverter<Rect>,
            IConverter<RectInt>,
            IConverter<UnityEngine.Object>,
            IConverter<UnityEngine.GameObject>,
            IContravariantConverter<UnityEngine.Component>,
            IContravariantConverter<Enum>
        {
            public static Converter Default;


            readonly string IConverter<string>.Convert<T>(T value)
            {
                if (value == null) return string.Empty;
                return value?.ToString();
            }

            readonly int IConverter<int>.Convert<T>(T value)
            {
                if (value == null) return 0;
                if (value is int i)
                {
                    return i;
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
            }

            readonly float IConverter<float>.Convert<T>(T value)
            {
                if (value == null) return 0;
                if (value is float f)
                {
                    return f;
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
            }

            readonly bool IConverter<bool>.Convert<T>(T value)
            {
                if (value == null) return false;
                if (value is bool b)
                {
                    return b;
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
            }

            readonly Vector2 IConverter<Vector2>.Convert<T>(T value)
            {
                if (value == null) return Vector2.zero;
                if (value is Vector2 v2)
                {
                    return v2;
                }
                if (value is Color color)
                {
                    return (Vector4)color;
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

            readonly Vector3 IConverter<Vector3>.Convert<T>(T value)
            {
                if (value == null) return Vector3.zero;
                if (value is Vector3 vector3)
                {
                    return vector3;
                }
                if (value is Color color)
                {
                    return (Vector4)color;
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

            readonly Vector4 IConverter<Vector4>.Convert<T>(T value)
            {
                if (value == null) return Vector4.zero;
                if (value is Vector4 vector4)
                {
                    return vector4;
                }
                if (value is Color color)
                {
                    return color;
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

            /// <summary>
            /// Color convert, use vector 4
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="value"></param>
            /// <returns></returns>
            readonly Color IConverter<Color>.Convert<T>(T value)
            {
                var converter = (IConverter<Vector4>)this;
                return converter.Convert(value);
            }

            readonly RectInt IConverter<RectInt>.Convert<T>(T value)
            {
                var v4 = ((IConverter<Vector4>)this).Convert(value);
                return new RectInt((int)v4.x, (int)v4.y, (int)v4.z, (int)v4.w);
            }

            readonly Rect IConverter<Rect>.Convert<T>(T value)
            {
                var v4 = ((IConverter<Vector4>)this).Convert(value);
                return new Rect(v4.x, v4.y, v4.z, v4.w);
            }



            private static UnityEngine.Object ConvertToUnityObject<T>(T value)
            {
                if (value == null) return null;
                return value is UnityEngine.Object obj ? obj : throw new InvalidCastException();
            }

            readonly object IContravariantConverter<Component>.Convert<TTargetValue, T>(T target)
            {
                var unityObject = ConvertToUnityObject(target);
                if (unityObject == null) return null;
                switch (unityObject)
                {
                    case GameObject gameObject:
                        return gameObject.GetComponent<TTargetValue>();
                    case Component component:
                        if (component is TTargetValue targetValue) return targetValue;
                        return component.GetComponent<TTargetValue>();
                    default:
                        break;
                }
                throw InvalidCast<TTargetValue>(target);
            }

            readonly UnityEngine.GameObject IConverter<UnityEngine.GameObject>.Convert<T>(T value)
            {
                var unityObject = ConvertToUnityObject(value);
                if (unityObject == null) return null;
                switch (unityObject)
                {
                    case GameObject gameObject:
                        return gameObject;
                    case Component component:
                        return component.gameObject;
                    default:
                        break;
                }
                throw InvalidCast<GameObject>(value);
            }

            readonly UnityEngine.Object IConverter<UnityEngine.Object>.Convert<T>(T value)
            {
                return ConvertToUnityObject(value);
            }

            readonly object IContravariantConverter<Enum>.Convert<TTargetValue, T>(T value)
            {
                string v = ((IConverter<int>)this).Convert(value).ToString();
                return Enum.TryParse(typeof(TTargetValue), v, out var e) ? e : 0;
            }

            public readonly unsafe TResult ConvertToEnum<TResult, TValue>(TValue value) where TResult : unmanaged, Enum
            {
                switch (value)
                {
                    case int i:
                        return *(TResult*)&i;
                    case string str:
                        return Enum.Parse<TResult>(str);
                }
                try
                {
                    int i = ((IConverter<int>)this).Convert(value);
                    return *(TResult*)&i;
                }
                catch { }
                string s = ((IConverter<string>)this).Convert(value);
                return Enum.Parse<TResult>(s);
            }

            public readonly unsafe TResult ConvertTo<TResult, TValue>(TValue value)
            {
                switch (value)
                {
                    case int i:
                        return (TResult)Enum.ToObject(typeof(TResult), value);
                    case string str:
                        return (TResult)Enum.Parse(typeof(TResult), str);
                }
                try
                {
                    return (TResult)Enum.ToObject(typeof(TResult), value);
                }
                catch { }
                string s = ((IConverter<string>)this).Convert(value);
                return (TResult)Enum.Parse(typeof(TResult), s);
            }
        }

        static InvalidCastException InvalidCast<T>(object value) => InvalidCast(typeof(T).FullName, value);
        static InvalidCastException InvalidCast(string type, object value)
        {
            return new InvalidCastException($"{value} cannot be casted to {type}");
        }
    }
}
