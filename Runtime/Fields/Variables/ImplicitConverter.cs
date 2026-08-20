using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>Converts a value to a requested variable target type.</summary>
    public static class ImplicitConverter<TTarget>
    {
        static ImplicitConverter()
        {
            BuiltInConversionRegistration.EnsureInitialized();
        }

        /// <summary>Checks whether a source type has a conversion rule for this target type.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanConvertFrom<TSource>()
        {
            return ConversionPair<TSource, TTarget>.Rule.IsSupported;
        }

        /// <summary>Attempts to convert a value without allocating an exception on failure.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFrom<TSource>(TSource source, out TTarget result)
        {
            return ConversionPair<TSource, TTarget>.TryConvert(source, out result);
        }

        /// <summary>Converts a value or throws when the conversion is unsupported or fails.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TTarget From<TSource>(TSource source)
        {
            if (TryFrom(source, out TTarget result))
            {
                return result;
            }

            throw new InvalidCastException(
                $"Cannot convert {typeof(TSource).FullName} to {typeof(TTarget).FullName}.");
        }
    }

    internal delegate bool TryConversion<TSource, TTarget>(TSource source, out TTarget result);

    internal readonly struct ConversionRule<TSource, TTarget>
    {
        private readonly TryConversion<TSource, TTarget> converter;

        internal ConversionRule(TryConversion<TSource, TTarget> converter)
        {
            this.converter = converter;
        }

        internal bool IsSupported => converter != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryConvert(TSource source, out TTarget result)
        {
            if (converter == null)
            {
                result = default;
                return false;
            }

            return converter(source, out result);
        }
    }

    internal static class ConversionPair<TSource, TTarget>
    {
        private static ConversionRule<TSource, TTarget> registeredRule;

        private static class Finalized
        {
            internal static readonly ConversionRule<TSource, TTarget> Rule =
                registeredRule.IsSupported
                    ? registeredRule
                    : StructuralConversion<TSource, TTarget>.Create();
        }

        internal static ConversionRule<TSource, TTarget> Rule => Finalized.Rule;

        internal static void Register(TryConversion<TSource, TTarget> converter)
        {
            ConversionRegistry.ThrowIfFrozen();
            if (registeredRule.IsSupported)
            {
                throw new InvalidOperationException(
                    $"Conversion from {typeof(TSource).FullName} to {typeof(TTarget).FullName} is already registered.");
            }

            registeredRule = new ConversionRule<TSource, TTarget>(converter);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryConvert(TSource source, out TTarget result)
        {
            return Finalized.Rule.TryConvert(source, out result);
        }
    }

    internal static class ConversionRegistry
    {
        private static bool frozen;

        internal static void Freeze() => frozen = true;

        internal static void ThrowIfFrozen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("Variable conversion registry is frozen.");
            }
        }
    }

    internal static class BuiltInConversionRegistration
    {
        private static readonly bool initialized = Initialize();

        internal static void EnsureInitialized()
        {
            _ = initialized;
        }

        private static bool Initialize()
        {
            RegisterBuiltIns();
            ConversionRegistry.Freeze();
            return true;
        }

        private static void RegisterBuiltIns()
        {
            Register<int, float>(NumericConversions.IntToFloat);
            Register<float, int>(NumericConversions.FloatToInt);
            Register<bool, int>(NumericConversions.BoolToInt);
            Register<bool, float>(NumericConversions.BoolToFloat);
            Register<LayerMask, int>(NumericConversions.LayerMaskToInt);
            Register<int, LayerMask>(NumericConversions.IntToLayerMask);

            Register<int, Vector2>(VectorConversions.IntToVector2);
            Register<int, Vector3>(VectorConversions.IntToVector3);
            Register<int, Vector4>(VectorConversions.IntToVector4);
            Register<float, Vector2>(VectorConversions.FloatToVector2);
            Register<float, Vector3>(VectorConversions.FloatToVector3);
            Register<float, Vector4>(VectorConversions.FloatToVector4);
            Register<bool, Vector2>(VectorConversions.BoolToVector2);
            Register<bool, Vector3>(VectorConversions.BoolToVector3);
            Register<bool, Vector4>(VectorConversions.BoolToVector4);

            Register<Vector2, Vector3>(VectorConversions.Vector2ToVector3);
            Register<Vector2, Vector4>(VectorConversions.Vector2ToVector4);
            Register<Vector3, Vector2>(VectorConversions.Vector3ToVector2);
            Register<Vector3, Vector4>(VectorConversions.Vector3ToVector4);
            Register<Vector4, Vector2>(VectorConversions.Vector4ToVector2);
            Register<Vector4, Vector3>(VectorConversions.Vector4ToVector3);

            Register<Color, Vector2>(ColorConversions.ColorToVector2);
            Register<Color, Vector3>(ColorConversions.ColorToVector3);
            Register<Color, Vector4>(ColorConversions.ColorToVector4);
            Register<Vector2, Color>(ColorConversions.Vector2ToColor);
            Register<Vector3, Color>(ColorConversions.Vector3ToColor);
            Register<Vector4, Color>(ColorConversions.Vector4ToColor);

            Register<Vector2Int, Vector2>(VectorConversions.Vector2IntToVector2);
            Register<Vector3Int, Vector2>(VectorConversions.Vector3IntToVector2);
            Register<Vector2Int, Vector3>(VectorConversions.Vector2IntToVector3);
            Register<Vector3Int, Vector3>(VectorConversions.Vector3IntToVector3);
            Register<Vector2Int, Vector4>(VectorConversions.Vector2IntToVector4);
            Register<Vector3Int, Vector4>(VectorConversions.Vector3IntToVector4);
        }

        private static void Register<TSource, TTarget>(TryConversion<TSource, TTarget> converter)
        {
            ConversionPair<TSource, TTarget>.Register(converter);
        }
    }

    internal static class NumericConversions
    {
        internal static bool IntToFloat(int source, out float result) { result = source; return true; }
        internal static bool FloatToInt(float source, out int result) { result = (int)source; return true; }
        internal static bool BoolToInt(bool source, out int result) { result = source ? 1 : 0; return true; }
        internal static bool BoolToFloat(bool source, out float result) { result = source ? 1f : 0f; return true; }
        internal static bool LayerMaskToInt(LayerMask source, out int result) { result = source.value; return true; }
        internal static bool IntToLayerMask(int source, out LayerMask result) { result = new LayerMask { value = source }; return true; }
    }

    internal static class VectorConversions
    {
        internal static bool IntToVector2(int source, out Vector2 result) { result = Vector2.one * source; return true; }
        internal static bool IntToVector3(int source, out Vector3 result) { result = Vector3.one * source; return true; }
        internal static bool IntToVector4(int source, out Vector4 result) { result = Vector4.one * source; return true; }
        internal static bool FloatToVector2(float source, out Vector2 result) { result = Vector2.one * source; return true; }
        internal static bool FloatToVector3(float source, out Vector3 result) { result = Vector3.one * source; return true; }
        internal static bool FloatToVector4(float source, out Vector4 result) { result = Vector4.one * source; return true; }
        internal static bool BoolToVector2(bool source, out Vector2 result) { result = source ? Vector2.one : Vector2.zero; return true; }
        internal static bool BoolToVector3(bool source, out Vector3 result) { result = source ? Vector3.one : Vector3.zero; return true; }
        internal static bool BoolToVector4(bool source, out Vector4 result) { result = source ? Vector4.one : Vector4.zero; return true; }
        internal static bool Vector2ToVector3(Vector2 source, out Vector3 result) { result = source; return true; }
        internal static bool Vector2ToVector4(Vector2 source, out Vector4 result) { result = source; return true; }
        internal static bool Vector3ToVector2(Vector3 source, out Vector2 result) { result = source; return true; }
        internal static bool Vector3ToVector4(Vector3 source, out Vector4 result) { result = source; return true; }
        internal static bool Vector4ToVector2(Vector4 source, out Vector2 result) { result = source; return true; }
        internal static bool Vector4ToVector3(Vector4 source, out Vector3 result) { result = source; return true; }
        internal static bool Vector2IntToVector2(Vector2Int source, out Vector2 result) { result = source; return true; }
        internal static bool Vector3IntToVector2(Vector3Int source, out Vector2 result) { result = (Vector2)(Vector3)source; return true; }
        internal static bool Vector2IntToVector3(Vector2Int source, out Vector3 result) { result = (Vector3)(Vector2)source; return true; }
        internal static bool Vector3IntToVector3(Vector3Int source, out Vector3 result) { result = source; return true; }
        internal static bool Vector2IntToVector4(Vector2Int source, out Vector4 result) { result = (Vector4)(Vector2)source; return true; }
        internal static bool Vector3IntToVector4(Vector3Int source, out Vector4 result) { result = (Vector4)(Vector3)source; return true; }
    }

    internal static class ColorConversions
    {
        internal static bool ColorToVector2(Color source, out Vector2 result) { result = (Vector2)(Vector4)source; return true; }
        internal static bool ColorToVector3(Color source, out Vector3 result) { result = (Vector3)(Vector4)source; return true; }
        internal static bool ColorToVector4(Color source, out Vector4 result) { result = source; return true; }
        internal static bool Vector2ToColor(Vector2 source, out Color result) { result = (Color)(Vector4)source; return true; }
        internal static bool Vector3ToColor(Vector3 source, out Color result) { result = (Color)(Vector4)source; return true; }
        internal static bool Vector4ToColor(Vector4 source, out Color result) { result = source; return true; }
    }

    internal static class StructuralConversion<TSource, TTarget>
    {
        internal static ConversionRule<TSource, TTarget> Create()
        {
            if (typeof(TSource) == typeof(TTarget))
            {
                return new ConversionRule<TSource, TTarget>(Identity);
            }

            if (typeof(TSource) == typeof(object))
            {
                return new ConversionRule<TSource, TTarget>(FromObject);
            }

            if (typeof(TTarget) == typeof(string))
            {
                return new ConversionRule<TSource, TTarget>(ToStringValue);
            }

            if (typeof(TTarget) == typeof(bool) &&
                (typeof(TSource) == typeof(int) || typeof(TSource) == typeof(float) ||
                 typeof(TSource) == typeof(Vector2) || typeof(TSource) == typeof(Vector3) ||
                 typeof(TSource) == typeof(Vector4) ||
                 typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource))))
            {
                return typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource))
                    ? new ConversionRule<TSource, TTarget>(UnityObjectToBool)
                    : new ConversionRule<TSource, TTarget>(ToBoolValue);
            }

            if (typeof(TTarget) == typeof(Color) &&
                (typeof(TSource) == typeof(Vector2) || typeof(TSource) == typeof(Vector3) ||
                 typeof(TSource) == typeof(Vector4)))
            {
                return new ConversionRule<TSource, TTarget>(VectorToColor);
            }

            if (typeof(TTarget) == typeof(Rect) && SupportsVector4Source())
                return new ConversionRule<TSource, TTarget>(ToRect);

            if (typeof(TTarget) == typeof(RectInt) && SupportsVector4Source())
                return new ConversionRule<TSource, TTarget>(ToRectInt);

            if (typeof(TSource).IsEnum && IsNumericType(typeof(TTarget)))
            {
                return new ConversionRule<TSource, TTarget>(EnumToNumeric);
            }

            if (typeof(TTarget).IsEnum &&
                (IsNumericType(typeof(TSource)) || typeof(TSource) == typeof(string)))
            {
                return new ConversionRule<TSource, TTarget>(NumericOrStringToEnum);
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource)))
            {
                if (typeof(TTarget) == typeof(int) || typeof(TTarget) == typeof(float))
                {
                    return new ConversionRule<TSource, TTarget>(UnityObjectToNumeric);
                }
                if (typeof(TTarget) == typeof(Vector2) || typeof(TTarget) == typeof(Vector3) || typeof(TTarget) == typeof(Vector4))
                {
                    return new ConversionRule<TSource, TTarget>(UnityObjectToVector);
                }
                if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TTarget)))
                {
                    return new ConversionRule<TSource, TTarget>(UnityObjectToUnityObject);
                }
            }

            if (typeof(TTarget) == typeof(UnityEngine.Object) ||
                typeof(UnityEngine.Object).IsAssignableFrom(typeof(TTarget)))
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource)))
                {
                    return new ConversionRule<TSource, TTarget>(UnityObjectToUnityObject);
                }
            }

            if (!typeof(TSource).IsValueType && !typeof(TTarget).IsValueType &&
                typeof(TTarget).IsAssignableFrom(typeof(TSource)))
            {
                return new ConversionRule<TSource, TTarget>(ReferenceCast);
            }

            return default;
        }

        private static bool SupportsVector4Source()
        {
            return typeof(TSource) == typeof(object)
                || typeof(TSource) == typeof(int)
                || typeof(TSource) == typeof(float)
                || typeof(TSource) == typeof(bool)
                || typeof(TSource) == typeof(Vector2)
                || typeof(TSource) == typeof(Vector3)
                || typeof(TSource) == typeof(Vector4)
                || typeof(TSource) == typeof(Color)
                || typeof(TSource) == typeof(Vector2Int)
                || typeof(TSource) == typeof(Vector3Int)
                || typeof(UnityEngine.Object).IsAssignableFrom(typeof(TSource));
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(char);
        }

        private static bool Identity(TSource source, out TTarget result)
        {
            result = UnsafeUtility.As<TSource, TTarget>(ref source);
            return true;
        }

        private static bool ReferenceCast(TSource source, out TTarget result)
        {
            result = (TTarget)(object)source;
            return true;
        }

        private static bool FromObject(TSource source, out TTarget result)
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

        private static bool ToStringValue(TSource source, out TTarget result)
        {
            object value = source;
            result = (TTarget)(object)(value?.ToString() ?? string.Empty);
            return true;
        }

        private static bool ToBoolValue(TSource source, out TTarget result)
        {
            bool value;
            if (typeof(TSource) == typeof(int)) value = UnsafeUtility.As<TSource, int>(ref source) != 0;
            else if (typeof(TSource) == typeof(float)) value = UnsafeUtility.As<TSource, float>(ref source) != 0;
            else if (typeof(TSource) == typeof(Vector2)) value = UnsafeUtility.As<TSource, Vector2>(ref source) != Vector2.zero;
            else if (typeof(TSource) == typeof(Vector3)) value = UnsafeUtility.As<TSource, Vector3>(ref source) != Vector3.zero;
            else value = UnsafeUtility.As<TSource, Vector4>(ref source) != Vector4.zero;
            result = UnsafeUtility.As<bool, TTarget>(ref value);
            return true;
        }

        private static bool UnityObjectToBool(TSource source, out TTarget result)
        {
            UnityEngine.Object value = (UnityEngine.Object)(object)source;
            bool converted = value;
            result = UnsafeUtility.As<bool, TTarget>(ref converted);
            return true;
        }

        private static bool VectorToColor(TSource source, out TTarget result)
        {
            Color color;
            if (typeof(TSource) == typeof(Vector2))
                color = (Color)(Vector4)UnsafeUtility.As<TSource, Vector2>(ref source);
            else if (typeof(TSource) == typeof(Vector3))
                color = (Color)(Vector4)UnsafeUtility.As<TSource, Vector3>(ref source);
            else
                color = (Color)UnsafeUtility.As<TSource, Vector4>(ref source);

            result = UnsafeUtility.As<Color, TTarget>(ref color);
            return true;
        }

        private static bool ToRect(TSource source, out TTarget result)
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

        private static bool ToRectInt(TSource source, out TTarget result)
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

        private static bool EnumToNumeric(TSource source, out TTarget result)
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

        private static bool NumericOrStringToEnum(TSource source, out TTarget result)
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

        private static bool UnityObjectToNumeric(TSource source, out TTarget result)
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

        private static bool UnityObjectToVector(TSource source, out TTarget result)
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

        private static bool UnityObjectToUnityObject(TSource source, out TTarget result)
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
                Component converted = component != null && typeof(TTarget).IsInstanceOfType(component)
                    ? component
                    : (value as GameObject)?.GetComponent(typeof(TTarget));
                result = (TTarget)(object)converted;
                return converted != null;
            }

            result = (TTarget)(object)value;
            return typeof(TTarget).IsInstanceOfType(value);
        }
    }
}
