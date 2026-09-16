using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB) in 2D space, defined by its minimum and maximum corners.
    /// </summary>
    public struct AABB : IEquatable<AABB>
    {
        public Vector2 Min { get; set; }
        public Vector2 Max { get; set; }


        public readonly float SizeX => Max.x - Min.x;
        public readonly float SizeY => Max.y - Min.y;
        public readonly Vector2 Size => Max - Min;
        public readonly Vector2 Center => Min + Size * 0.5f;
        public readonly float CenterX => Min.x + SizeX * 0.5f;
        public readonly float CenterY => Min.y + SizeY * 0.5f;

        /// <summary>Gets the continuous horizontal center and the lower edge, without integer rounding.</summary>
        public readonly Vector2 LowerCenter => new(CenterX, Min.y);



        public AABB(Vector2 min, Vector2 max)
        {
            if (min.x > max.x || min.y > max.y)
                throw new ArgumentException("Min must be less than or equal to Max.");
            Min = min;
            Max = max;
        }

        public AABB(float minX, float minY, float maxX, float maxY) : this(new Vector2(minX, minY), new Vector2(maxX, maxY)) { }


        public readonly bool Contains(Vector2 point) => point.x >= Min.x && point.x <= Max.x && point.y >= Min.y && point.y <= Max.y;
        public override readonly string ToString() => $"AABB(Min: {Min}, Max: {Max})";

        /// <summary>
        /// Compares every corner component approximately, within a given tolerance.
        /// This is useful for floating-point comparisons where exact equality may not be reliable.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Approximately(AABB other, float tolerance = 1e-5f)
        {
            return Mathf.Abs(Min.x - other.Min.x) <= tolerance &&
                   Mathf.Abs(Min.y - other.Min.y) <= tolerance &&
                   Mathf.Abs(Max.x - other.Max.x) <= tolerance &&
                   Mathf.Abs(Max.y - other.Max.y) <= tolerance;
        }

        /// <summary>
        /// Compares every corner component exactly. Unity's Vector2 equality operator is approximate and
        /// would make two distinct goal identities compare equal, so component equality is used instead.
        /// </summary>
        public readonly bool Equals(AABB other) => Min.Equals(other.Min) && Max.Equals(other.Max);

        public readonly override bool Equals(object obj) => obj is AABB other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(Min, Max);



        /// <summary>
        /// Creates a new AABB by translating the current AABB by the specified translation vector.
        /// </summary>
        /// <param name="translation"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AABB Translate(Vector2 translation) => new AABB(Min + translation, Max + translation);

        /// <summary>
        /// Translates the current AABB in place by the specified translation vector, modifying its minimum and maximum corners.
        /// </summary>
        /// <param name="translation"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InplaceTranslate(Vector2 translation) => this = Translate(translation);





        /// <summary>
        /// Creates an AABB from a center point and a size vector. The minimum and maximum corners are calculated as center - size/2 and center + size/2, respectively.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromCenterAndSize(Vector2 center, Vector2 size)
        {
            Vector2 halfSize = size * 0.5f;
            return new AABB(center - halfSize, center + halfSize);
        }

        /// <summary>
        /// Creates an AABB from a center point and a size vector. The minimum and maximum corners are calculated as center - size/2 and center + size/2, respectively.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="sizeX"></param>
        /// <param name="sizeY"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromCenterAndSize(Vector2 center, float sizeX, float sizeY)
        {
            Vector2 halfSize = new Vector2(sizeX * 0.5f, sizeY * 0.5f);
            return new AABB(center - halfSize, center + halfSize);
        }

        /// <summary>
        /// Creates an AABB from a center point and a uniform size. The minimum and maximum corners are calculated as center - size/2 and center + size/2, respectively.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromCenterAndSize(Vector2 center, float size) => FromCenterAndSize(center, size, size);

        /// <summary>
        /// Creates an AABB from a minimum corner(position) and a size vector. The maximum corner is calculated as min + size.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromMinAndSize(Vector2 min, Vector2 size) => new AABB(min, min + size);

        /// <summary>
        /// Creates degenerate goal geometry for one continuous world-space point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Point(Vector2 point) => new(point, point);





        public static bool operator ==(AABB left, AABB right) => left.Equals(right);
        public static bool operator !=(AABB left, AABB right) => !left.Equals(right);

        public static implicit operator Bounds(AABB aabb)
        {
            Vector2 size = aabb.Max - aabb.Min;
            Vector2 center = aabb.Min + size * 0.5f;
            return new Bounds(center, size);
        }

        public static implicit operator AABB(Bounds bounds) => new AABB(bounds.min, bounds.max);

        public static implicit operator Rect(AABB aabb) => new Rect(aabb.Min, aabb.Size);

        public static implicit operator AABB(Rect rect) => new AABB(rect.min, rect.max);
    }
}
