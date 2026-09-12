using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Centralizes stateless input validation shared by navigation contracts, planning, and execution.
    /// </summary>
    public static class Validate
    {
        /// <summary>
        /// Rejects a non-finite or negative scalar with the standard navigation message.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NonNegativeFinite(float value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value)) throw new ArgumentException("value must be finite.", parameterName);
            if (value < 0f) throw new ArgumentOutOfRangeException(parameterName, value, "value cannot be negative.");
        }

        /// <summary>
        /// Rejects a non-finite or non-positive scalar navigation value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PositiveFinite(float value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value)) throw new ArgumentException("value must be finite.", parameterName);
            if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, value, "value must be finite and positive.");
        }




        /// <summary>
        /// Rejects a non-finite navigation coordinate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Finite(Vector2 value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value))
                throw new ArgumentException("Navigation coordinates must be finite.", parameterName);
        }

        /// <summary>
        /// Rejects a non-finite or non-positive vector navigation value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PositiveVector(Vector2 value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value) || value.x <= 0 || value.y <= 0)
                throw new ArgumentException("Body size must be finite and positive.", parameterName);
        }

        /// <summary>
        /// Rejects a non-finite vector with negative components.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NonNegativeVector(Vector2 value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value) || value.x < 0f || value.y < 0f)
                throw new ArgumentException("Body size must be finite and non-negative.", parameterName);
        }




        /// <summary>
        /// Rejects non-finite bounds or bounds with negative extents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Bounds(Bounds value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value) || value.size.x < 0f || value.size.y < 0f || value.size.z < 0f)
                throw new ArgumentException("Goal bounds must be finite and non-negative.", parameterName);
        }




        /// <summary>
        /// Rejects a non-finite rectangle with negative dimensions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NonNegativeRect(Rect value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value) || value.width < 0f || value.height < 0f)
                throw new ArgumentException("Rectangle dimensions must be finite and non-negative.", parameterName);
        }

        /// <summary>
        /// Rejects a non-finite or non-positive rectangle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PositiveRect(Rect value, string parameterName)
        {
            NonNegativeRect(value, parameterName);
            if (value.width <= 0f || value.height <= 0f)
                throw new ArgumentException("Rectangle dimensions must be positive.", parameterName);
        }
    }
}
