using Aethiumian.AI.Variables;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Provides allocation-free typed access and shape resolution for arithmetic nodes.
    /// </summary>
    internal static class ArithmeticCompatibility
    {
        /// <summary>
        /// Determines whether the variable type is a scalar numeric type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsScalar(VariableType type)
        {
            return type == VariableType.Int || type == VariableType.Float;
        }

        /// <summary>
        /// Determines whether the variable type is a supported floating-point vector type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsVector(VariableType type)
        {
            return type == VariableType.Vector2
                || type == VariableType.Vector3
                || type == VariableType.Vector4;
        }

        /// <summary>
        /// Resolves the result type for a component-wise binary operation.
        /// Scalars broadcast to vectors; vector widths must otherwise match.
        /// </summary>
        public static bool TryResolveComponentwiseType(
            VariableType left,
            VariableType right,
            out VariableType resultType)
        {
            if (IsScalar(left) && IsScalar(right))
            {
                resultType = left == VariableType.Float || right == VariableType.Float
                    ? VariableType.Float
                    : VariableType.Int;
                return true;
            }

            if (IsVector(left) && IsScalar(right))
            {
                resultType = left;
                return true;
            }

            if (IsScalar(left) && IsVector(right))
            {
                resultType = right;
                return true;
            }

            if (IsVector(left) && left == right)
            {
                resultType = left;
                return true;
            }

            resultType = VariableType.Invalid;
            return false;
        }

    }
}
