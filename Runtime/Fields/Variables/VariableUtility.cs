using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using static Aethiumian.AI.Variables.VectorUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Variable Utility class that handle variable casting in the system
    /// </summary>
    public static class VariableUtility
    {
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
                    if (TryParseVector2(value, out Vector2 vector2)) return vector2;
                    throw new FormatException($"Cannot parse '{value}' as Vector2.");
                case VariableType.Vector3:
                    if (TryParseVector3(value, out Vector3 vector3)) return vector3;
                    throw new FormatException($"Cannot parse '{value}' as Vector3.");
                case VariableType.Vector4:
                    if (TryParseVector4(value, out Vector4 vector4)) return vector4;
                    throw new FormatException($"Cannot parse '{value}' as Vector4.");
                default:
                    break;
            }
            return null;
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
        /// <returns></returns>
        public static Variable Create(VariableData data, object target)
        {
            if (data.IsScript)
            {
                return new TargetScriptVariable(data, target);
            }
            return new TreeVariable(data);
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

        /// <summary>
        /// Implicit converstion between supported variables
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static TResult ImplicitConversion<TResult, TValue>(TValue value) => ImplicitConverter<TResult>.From(value);

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

            if (restrictedType.IsInstanceOfType(value))
            {
                return value;
            }

            if (restrictedType == typeof(int)) return ImplicitConverter<int>.From(value);
            if (restrictedType == typeof(float)) return ImplicitConverter<float>.From(value);
            if (restrictedType == typeof(string)) return ImplicitConverter<string>.From(value);
            if (restrictedType == typeof(bool)) return ImplicitConverter<bool>.From(value);
            if (restrictedType == typeof(Vector2)) return ImplicitConverter<Vector2>.From(value);
            if (restrictedType == typeof(Vector3)) return ImplicitConverter<Vector3>.From(value);
            if (restrictedType == typeof(Vector4)) return ImplicitConverter<Vector4>.From(value);
            if (restrictedType == typeof(Color)) return ImplicitConverter<Color>.From(value);
            if (restrictedType == typeof(Rect)) return ImplicitConverter<Rect>.From(value);
            if (restrictedType == typeof(RectInt)) return ImplicitConverter<RectInt>.From(value);
            if (restrictedType == typeof(LayerMask)) return ImplicitConverter<LayerMask>.From(value);
            if (restrictedType.IsEnum)
            {
                int numericValue = ImplicitConverter<int>.From(value);
                return Enum.ToObject(restrictedType, numericValue);
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






        public static IReadOnlyList<VariableType> GetCompatibleTypes(VariableType type) => VariableTypeCatalog.GetCompatibleTypes(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableType GetVariableType<T>() => VariableTypeCatalog.Of<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableType GetVariableType(Type restrictedType) => VariableTypeCatalog.Of(restrictedType);

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
        public static VariableType GetType(object value) => VariableTypeCatalog.Of(value);





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

    }
}
