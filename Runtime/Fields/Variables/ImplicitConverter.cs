using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>Converts a value to a requested variable target type.</summary>
    public static class ImplicitConverter<TTarget>
    {
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
            return ConversionPair<TSource, TTarget>.Rule.TryConvert(source, out result);
        }

        /// <summary>Converts a value or throws when the conversion is unsupported or fails.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TTarget From<TSource>(TSource source)
        {
            if (TryFrom(source, out TTarget result))
            {
                return result;
            }

            throw new InvalidCastException($"Cannot convert {typeof(TSource).FullName} to {typeof(TTarget).FullName}.");
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
        internal static readonly ConversionRule<TSource, TTarget> Rule = ConversionRegistry.Resolve<TSource, TTarget>();
    }

    /// <summary>Finds immutable conversion definitions shared by support queries, execution, and authoring explanations.</summary>
    public static class ConversionRegistry
    {
        // Exact pairs precede structural families so support queries and execution select the same definition.
        private static readonly ConversionDefinition[] Definitions = {
            new ConversionDefinition<int, float>(NumericConversions.IntToFloat, "Convert the integer to Float; large integers can lose precision."),
            new ConversionDefinition<float, int>(NumericConversions.FloatToInt, "Convert Float to Int by discarding the fractional part."),
            new ConversionDefinition<bool, int>(NumericConversions.BoolToInt, "Use 1 for true and 0 for false."),
            new ConversionDefinition<bool, float>(NumericConversions.BoolToFloat, "Use 1 for true and 0 for false."),
            new ConversionDefinition<LayerMask, int>(NumericConversions.LayerMaskToInt, "Read the layer mask's integer bit pattern."),
            new ConversionDefinition<int, LayerMask>(NumericConversions.IntToLayerMask, "Use the integer as the layer mask's bit pattern."),
            new ConversionDefinition<int, Vector2>(VectorConversions.IntToVector2, "Use the integer for X and Y; large integers can lose precision."),
            new ConversionDefinition<int, Vector3>(VectorConversions.IntToVector3, "Use the integer for X, Y, and Z; large integers can lose precision."),
            new ConversionDefinition<int, Vector4>(VectorConversions.IntToVector4, "Use the integer for X, Y, Z, and W; large integers can lose precision."),
            new ConversionDefinition<float, Vector2>(VectorConversions.FloatToVector2, "Use the Float value for X and Y."),
            new ConversionDefinition<float, Vector3>(VectorConversions.FloatToVector3, "Use the Float value for X, Y, and Z."),
            new ConversionDefinition<float, Vector4>(VectorConversions.FloatToVector4, "Use the Float value for X, Y, Z, and W."),
            new ConversionDefinition<bool, Vector2>(VectorConversions.BoolToVector2, "Fill X and Y with 1 for true or 0 for false."),
            new ConversionDefinition<bool, Vector3>(VectorConversions.BoolToVector3, "Fill X, Y, and Z with 1 for true or 0 for false."),
            new ConversionDefinition<bool, Vector4>(VectorConversions.BoolToVector4, "Fill X, Y, Z, and W with 1 for true or 0 for false."),
            new ConversionDefinition<Vector2, Vector3>(VectorConversions.Vector2ToVector3, "Preserve X and Y; set Z to 0."),
            new ConversionDefinition<Vector2, Vector4>(VectorConversions.Vector2ToVector4, "Preserve X and Y; set Z and W to 0."),
            new ConversionDefinition<Vector3, Vector2>(VectorConversions.Vector3ToVector2, "Preserve X and Y; discard Z."),
            new ConversionDefinition<Vector3, Vector4>(VectorConversions.Vector3ToVector4, "Preserve X, Y, and Z; set W to 0."),
            new ConversionDefinition<Vector4, Vector2>(VectorConversions.Vector4ToVector2, "Preserve X and Y; discard Z and W."),
            new ConversionDefinition<Vector4, Vector3>(VectorConversions.Vector4ToVector3, "Preserve X, Y, and Z; discard W."),
            new ConversionDefinition<Color, Vector2>(ColorConversions.ColorToVector2, "Use R and G as X and Y; discard B and A."),
            new ConversionDefinition<Color, Vector3>(ColorConversions.ColorToVector3, "Use R, G, and B as X, Y, and Z; discard A."),
            new ConversionDefinition<Color, Vector4>(ColorConversions.ColorToVector4, "Use R, G, B, and A as X, Y, Z, and W."),
            new ConversionDefinition<Vector2, Color>(ColorConversions.Vector2ToColor, "Use X and Y as R and G; set B and A to 0."),
            new ConversionDefinition<Vector3, Color>(ColorConversions.Vector3ToColor, "Use X, Y, and Z as R, G, and B; set A to 0."),
            new ConversionDefinition<Vector4, Color>(ColorConversions.Vector4ToColor, "Use X, Y, Z, and W as R, G, B, and A."),
            new ConversionDefinition<Vector2Int, Vector2>(VectorConversions.Vector2IntToVector2, "Convert X and Y to Float; large integers can lose precision."),
            new ConversionDefinition<Vector3Int, Vector2>(VectorConversions.Vector3IntToVector2, "Convert X and Y to Float and discard Z; large integers can lose precision."),
            new ConversionDefinition<Vector2Int, Vector3>(VectorConversions.Vector2IntToVector3, "Convert X and Y to Float and set Z to 0; large integers can lose precision."),
            new ConversionDefinition<Vector3Int, Vector3>(VectorConversions.Vector3IntToVector3, "Convert X, Y, and Z to Float; large integers can lose precision."),
            new ConversionDefinition<Vector2Int, Vector4>(VectorConversions.Vector2IntToVector4, "Convert X and Y to Float and set Z and W to 0; large integers can lose precision."),
            new ConversionDefinition<Vector3Int, Vector4>(VectorConversions.Vector3IntToVector4, "Convert X, Y, and Z to Float and set W to 0; large integers can lose precision."),

            new StructuralConversions.IdentityDefinition(),
            new StructuralConversions.ObjectDispatchDefinition(),
            new StructuralConversions.StringDefinition(),
            new StructuralConversions.BooleanDefinition(),
            new StructuralConversions.RectangleDefinition(),
            new StructuralConversions.IntegerRectangleDefinition(),
            new StructuralConversions.EnumNumericDefinition(),
            new StructuralConversions.EnumDefinition(),
            new StructuralConversions.UnityNumericDefinition(),
            new StructuralConversions.UnityVectorDefinition(),
            new StructuralConversions.UnityReferenceDefinition(),
            new StructuralConversions.ReferenceDefinition()
        };

        /// <summary>Checks whether a definition supports a source-to-target conversion.</summary>
        /// <returns>Whether a definition exists; individual values can still fail object dispatch, enum parsing, or component lookup.</returns>
        /// <inheritdoc cref="Find(Type, Type)" path="/param"/>
        /// <inheritdoc cref="Find(Type, Type)" path="/exception"/>
        public static bool CanConvert(Type source, Type target) => Find(source, target) != null;

        /// <summary>Finds the complete definition that will bind this source-to-target conversion.</summary>
        /// <param name="source">The closed CLR source type, usable as a generic type argument.</param>
        /// <param name="target">The closed CLR target type, usable as a generic type argument.</param>
        /// <returns>The shared immutable definition, or null when the conversion is unsupported. Describe must receive the same matching types.</returns>
        /// <remarks>Exact definitions take precedence, followed by structural definitions in their declared order. Finding a definition does not guarantee every value converts successfully.</remarks>
        /// <exception cref="ArgumentNullException">Either type is null.</exception>
        /// <exception cref="ArgumentException">Either type is open, void, a pointer, a by-reference type, or a by-reference-like type.</exception>
        public static ConversionDefinition Find(Type source, Type target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (source.ContainsGenericParameters || target.ContainsGenericParameters
                || source == typeof(void) || target == typeof(void)
                || source.IsPointer || target.IsPointer || source.IsByRef || target.IsByRef
                || source.IsByRefLike || target.IsByRefLike)
                throw new ArgumentException("Conversion queries require closed CLR types usable as generic arguments.");

            for (int i = 0; i < Definitions.Length; i++)
            {
                ConversionDefinition definition = Definitions[i];
                if (definition.Matches(source, target)) return definition;
            }
            return null;
        }

        /// <summary>Binds the matching definition once for a closed generic conversion pair.</summary>
        internal static ConversionRule<TSource, TTarget> Resolve<TSource, TTarget>()
        {
            ConversionDefinition definition = Find(typeof(TSource), typeof(TTarget));
            return definition == null ? default : definition.Bind<TSource, TTarget>();
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

}
