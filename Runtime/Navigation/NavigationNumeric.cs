using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Provides the shared finite-value predicates used by navigation contracts,
    /// planners, and traversal executors.
    /// </summary>
    public static class NavigationNumeric
    {
        /// <summary>Returns whether the scalar is neither NaN nor infinite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float value) => float.IsFinite(value);

        /// <summary>Returns whether the double-precision scalar is neither NaN nor infinite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) => double.IsFinite(value);

        /// <summary>Returns whether both vector components are finite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        /// <summary>Returns whether all vector components are finite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        /// <summary>Returns whether a rectangle's position and size are finite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Rect value) => IsFinite(value.position) && IsFinite(value.size);

        /// <summary>Returns whether a bounds' center and size are finite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Bounds value) => IsFinite(value.center) && IsFinite(value.size);
    }
}
