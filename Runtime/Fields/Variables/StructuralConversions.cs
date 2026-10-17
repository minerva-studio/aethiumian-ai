using System;
using System.Globalization;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>Defines structural conversion families with their matching, execution, and explanation together.</summary>
    internal static class StructuralConversions
    {
        /// <summary>Checks the source families accepted by the existing Vector4-backed rectangle conversions.</summary>
        private static bool SupportsVector4Source(Type source)
        {
            return source == typeof(object) || source == typeof(int) || source == typeof(float) || source == typeof(bool)
                || source == typeof(Vector2) || source == typeof(Vector3) || source == typeof(Vector4) || source == typeof(Color)
                || source == typeof(Vector2Int) || source == typeof(Vector3Int) || typeof(UnityEngine.Object).IsAssignableFrom(source);
        }

        /// <summary>Checks the CLR numeric types supported by enum conversion.</summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
                || type == typeof(float) || type == typeof(double) || type == typeof(decimal) || type == typeof(char);
        }

        internal sealed class IdentityDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => source == target;

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Use the value unchanged.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                result = UnsafeUtility.As<TSource, TTarget>(ref source);
                return true;
            }
        }

        internal sealed class ObjectDispatchDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => source == typeof(object);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => $"Convert the Object value to {target.Name}; success depends on its runtime type and value.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                object value = source;
                if (value is null)
                {
                    result = default;
                    return !typeof(TTarget).IsValueType;
                }

                if (typeof(TTarget) == typeof(string))
                {
                    result = (TTarget)(object)value.ToString();
                    return true;
                }

                if (value is TTarget direct)
                {
                    result = direct;
                    return true;
                }

                if (value is int intValue)
                    return ImplicitConverter<TTarget>.TryFrom(intValue, out result);
                if (value is float floatValue)
                    return ImplicitConverter<TTarget>.TryFrom(floatValue, out result);
                if (value is bool boolValue)
                    return ImplicitConverter<TTarget>.TryFrom(boolValue, out result);
                if (value is Vector2 vector2Value)
                    return ImplicitConverter<TTarget>.TryFrom(vector2Value, out result);
                if (value is Vector3 vector3Value)
                    return ImplicitConverter<TTarget>.TryFrom(vector3Value, out result);
                if (value is Vector4 vector4Value)
                    return ImplicitConverter<TTarget>.TryFrom(vector4Value, out result);
                if (value is Color colorValue)
                    return ImplicitConverter<TTarget>.TryFrom(colorValue, out result);
                if (value is Vector2Int vector2IntValue)
                    return ImplicitConverter<TTarget>.TryFrom(vector2IntValue, out result);
                if (value is Vector3Int vector3IntValue)
                    return ImplicitConverter<TTarget>.TryFrom(vector3IntValue, out result);
                if (value is LayerMask layerMaskValue)
                    return ImplicitConverter<TTarget>.TryFrom(layerMaskValue, out result);
                if (value is string stringValue)
                    return ImplicitConverter<TTarget>.TryFrom(stringValue, out result);
                if (value is UnityEngine.Object unityObjectValue)
                    return ImplicitConverter<TTarget>.TryFrom(unityObjectValue, out result);

                result = default;
                return false;
            }
        }

        internal sealed class StringDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => target == typeof(string);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Format the value using ToString(); null becomes an empty string.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                object value = source;
                result = (TTarget)(object)(value?.ToString() ?? string.Empty);
                return true;
            }
        }

        internal sealed class BooleanDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => target == typeof(bool) && (source == typeof(int) || source == typeof(float) || source == typeof(Vector2) || source == typeof(Vector3) || source == typeof(Vector4) || typeof(UnityEngine.Object).IsAssignableFrom(source));

            /// <inheritdoc/>
            public override string Describe(Type source, Type target)
            {
                return typeof(UnityEngine.Object).IsAssignableFrom(source)
                    ? "Use true for a live Unity object and false for a null or destroyed object."
                    : "Use false when the scalar or vector compares equal to zero; otherwise use true.";
            }

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                bool value;
                if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource))) value = (UnityEngine.Object)(object)source;
                else if (typeof(TSource) == typeof(int)) value = UnsafeUtility.As<TSource, int>(ref source) != 0;
                else if (typeof(TSource) == typeof(float)) value = UnsafeUtility.As<TSource, float>(ref source) != 0;
                else if (typeof(TSource) == typeof(Vector2)) value = UnsafeUtility.As<TSource, Vector2>(ref source) != Vector2.zero;
                else if (typeof(TSource) == typeof(Vector3)) value = UnsafeUtility.As<TSource, Vector3>(ref source) != Vector3.zero;
                else value = UnsafeUtility.As<TSource, Vector4>(ref source) != Vector4.zero;
                result = UnsafeUtility.As<bool, TTarget>(ref value);
                return true;
            }
        }

        internal sealed class RectangleDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => target == typeof(Rect) && SupportsVector4Source(source);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Convert to Vector4, then use X, Y, Z, and W as the rectangle position and size.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                if (!ImplicitConverter<Vector4>.TryFrom(source, out Vector4 vector))
                {
                    result = default;
                    return false;
                }

                Rect rect = new(vector.x, vector.y, vector.z, vector.w);
                result = UnsafeUtility.As<Rect, TTarget>(ref rect);
                return true;
            }
        }

        internal sealed class IntegerRectangleDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => target == typeof(RectInt) && SupportsVector4Source(source);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Convert to Vector4 and discard each fractional part to obtain integer rectangle position and size.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                if (!ImplicitConverter<Vector4>.TryFrom(source, out Vector4 vector))
                {
                    result = default;
                    return false;
                }

                RectInt rect = new((int)vector.x, (int)vector.y, (int)vector.z, (int)vector.w);
                result = UnsafeUtility.As<RectInt, TTarget>(ref rect);
                return true;
            }
        }

        internal sealed class EnumNumericDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => source.IsEnum && IsNumericType(target);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => $"Convert the underlying enum value to {target.Name}; precision can be lost and out-of-range values can fail.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                try
                {
                    object converted = Convert.ChangeType(source, typeof(TTarget), CultureInfo.InvariantCulture);
                    result = (TTarget)converted;
                    return true;
                }
                catch
                {
                    result = default;
                    return false;
                }
            }
        }

        internal sealed class EnumDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => target.IsEnum && (IsNumericType(source) || source == typeof(string));

            /// <inheritdoc/>
            public override string Describe(Type source, Type target)
            {
                return source == typeof(string)
                    ? $"Parse an enum name or numeric text as {target.Name}; invalid text fails."
                    : $"Convert to the underlying numeric type of {target.Name}; the value need not name a defined member.";
            }

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                try
                {
                    object converted = typeof(TSource) == typeof(string)
                        ? Enum.Parse(typeof(TTarget), (string)(object)source)
                        : Enum.ToObject(
                            typeof(TTarget),
                            Convert.ChangeType(
                                source,
                                Enum.GetUnderlyingType(typeof(TTarget)),
                                CultureInfo.InvariantCulture));
                    result = (TTarget)converted;
                    return true;
                }
                catch
                {
                    result = default;
                    return false;
                }
            }
        }

        internal sealed class UnityNumericDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => typeof(UnityEngine.Object).IsAssignableFrom(source) && (target == typeof(int) || target == typeof(float));

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Use 1 for a live Unity object and 0 for a null or destroyed object.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                UnityEngine.Object value = (UnityEngine.Object)(object)source;
                if (typeof(TTarget) == typeof(int))
                {
                    int intValue = value ? 1 : 0;
                    result = UnsafeUtility.As<int, TTarget>(ref intValue);
                }
                else
                {
                    float floatValue = value ? 1f : 0f;
                    result = UnsafeUtility.As<float, TTarget>(ref floatValue);
                }
                return true;
            }
        }

        internal sealed class UnityVectorDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => typeof(UnityEngine.Object).IsAssignableFrom(source) && (target == typeof(Vector2) || target == typeof(Vector3) || target == typeof(Vector4));

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => "Fill the vector with 1 for a live Unity object or 0 for a null or destroyed object.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                UnityEngine.Object value = (UnityEngine.Object)(object)source;
                if (typeof(TTarget) == typeof(Vector2))
                {
                    Vector2 vector = value ? Vector2.one : Vector2.zero;
                    result = UnsafeUtility.As<Vector2, TTarget>(ref vector);
                }
                else if (typeof(TTarget) == typeof(Vector3))
                {
                    Vector3 vector = value ? Vector3.one : Vector3.zero;
                    result = UnsafeUtility.As<Vector3, TTarget>(ref vector);
                }
                else
                {
                    Vector4 vector = value ? Vector4.one : Vector4.zero;
                    result = UnsafeUtility.As<Vector4, TTarget>(ref vector);
                }
                return true;
            }
        }

        internal sealed class UnityReferenceDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => typeof(UnityEngine.Object).IsAssignableFrom(source) && typeof(UnityEngine.Object).IsAssignableFrom(target);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target)
            {
                if (target == typeof(GameObject)) return "Use the GameObject or a component's owning GameObject; other live Unity objects cannot convert.";
                if (typeof(Component).IsAssignableFrom(target))
                    return $"Reuse a matching component or find {target.Name} on a GameObject; a missing component fails.";
                return $"Use the Unity object as {target.Name}; a different actual object type can fail.";
            }

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                UnityEngine.Object value = (UnityEngine.Object)(object)source;
                if (value == null)
                {
                    result = default;
                    return true;
                }

                if (typeof(TTarget) == typeof(GameObject))
                {
                    if (value is GameObject go)
                    {
                        result = (TTarget)(object)go;
                        return true;
                    }
                    if (value is Component comp)
                    {
                        result = (TTarget)(object)comp.gameObject;
                        return true;
                    }
                    result = default;
                    return false;
                }

                if (typeof(Component).IsAssignableFrom(typeof(TTarget)))
                {
                    Component component = value as Component;
                    GameObject gameObject = value as GameObject;
                    Component converted = component != null && typeof(TTarget).IsInstanceOfType(component)
                        ? component
                        : gameObject != null ? gameObject.GetComponent(typeof(TTarget)) : null;
                    result = (TTarget)(object)converted;
                    return converted != null;
                }

                result = (TTarget)(object)value;
                return typeof(TTarget).IsInstanceOfType(value);
            }
        }

        internal sealed class ReferenceDefinition : ConversionDefinition
        {
            /// <inheritdoc/>
            public override bool Matches(Type source, Type target) => !source.IsValueType && !target.IsValueType && target.IsAssignableFrom(source);

            /// <inheritdoc/>
            public override string Describe(Type source, Type target) => $"Use the same object reference as {target.Name}; null remains null.";

            /// <inheritdoc/>
            internal override ConversionRule<TSource, TTarget> Bind<TSource, TTarget>() => new(TryConvert<TSource, TTarget>);

            /// <summary>Executes the operation for the generic pair already matched by this definition.</summary>
            private static bool TryConvert<TSource, TTarget>(TSource source, out TTarget result)
            {
                result = (TTarget)(object)source;
                return true;
            }
        }
    }
}
