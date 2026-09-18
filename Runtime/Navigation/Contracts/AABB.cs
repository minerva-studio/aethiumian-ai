using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Continuous axis-aligned box in world space, defined by its minimum and maximum corners.
    /// The box carries loose mutable semantics: it validates nothing, never normalizes its corners,
    /// and leaves range checking to the owner that authors the range.
    /// </summary>
    public struct AABB : IEquatable<AABB>
    {
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }


        /// <summary>Gets or sets the minimum corner. The components are the scalar storage above.</summary>
        public Vector2 Min
        {
            readonly get => new(MinX, MinY);
            set { MinX = value.x; MinY = value.y; }
        }

        /// <summary>Gets or sets the maximum corner. The components are the scalar storage above.</summary>
        public Vector2 Max
        {
            readonly get => new(MaxX, MaxY);
            set { MaxX = value.x; MaxY = value.y; }
        }

        public readonly float SizeX => MaxX - MinX;
        public readonly float SizeY => MaxY - MinY;
        public readonly Vector2 Size => new(MaxX - MinX, MaxY - MinY);
        public readonly Vector2 Center => new(MinX + (MaxX - MinX) * 0.5f, MinY + (MaxY - MinY) * 0.5f);
        public readonly float CenterX => MinX + (MaxX - MinX) * 0.5f;
        public readonly float CenterY => MinY + (MaxY - MinY) * 0.5f;

        /// <summary>Gets the continuous horizontal center and the lower edge, without integer rounding.</summary>
        public readonly Vector2 LowerCenter => new(CenterX, MinY);

        public AABB(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public AABB(Vector2 min, Vector2 max) : this(min.x, min.y, max.x, max.y) { }

        public readonly bool Contains(Vector2 point) => point.x >= MinX && point.x <= MaxX && point.y >= MinY && point.y <= MaxY;

        /// <summary>Returns whether two boxes overlap or touch, comparing corners inclusively.</summary>
        public readonly bool Intersects(AABB other) => MinX <= other.MaxX && other.MinX <= MaxX && MinY <= other.MaxY && other.MinY <= MaxY;

        /// <summary>Returns this box grown by the amount on every side.</summary>
        public readonly AABB Expand(float amountPerSide)
            => new(MinX - amountPerSide, MinY - amountPerSide, MaxX + amountPerSide, MaxY + amountPerSide);

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
            return Mathf.Abs(MinX - other.MinX) <= tolerance &&
                   Mathf.Abs(MinY - other.MinY) <= tolerance &&
                   Mathf.Abs(MaxX - other.MaxX) <= tolerance &&
                   Mathf.Abs(MaxY - other.MaxY) <= tolerance;
        }

        /// <summary>
        /// Compares every corner component exactly. Unity's Vector2 equality operator is approximate and
        /// would make two distinct goal identities compare equal, so the four scalars are compared instead.
        /// </summary>
        public readonly bool Equals(AABB other)
            => MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;

        public readonly override bool Equals(object obj) => obj is AABB other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);


        /// <summary>
        /// Creates a new AABB by translating the current AABB by the specified translation vector.
        /// </summary>
        /// <param name="translation"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AABB Translate(Vector2 translation) => new(MinX + translation.x, MinY + translation.y, MaxX + translation.x, MaxY + translation.y);

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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromCenterAndSize(float centerX, float centerY, float sizeX, float sizeY)
        {
            float halfSizeX = sizeX * 0.5f;
            float halfSizeY = sizeY * 0.5f;
            return new AABB(centerX - halfSizeX, centerY - halfSizeY, centerX + halfSizeX, centerY + halfSizeY);
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
        /// Creates an AABB from scalar minimum coordinates and scalar sizes; the maximum corner is min + size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromMinAndSize(float minX, float minY, float sizeX, float sizeY) => new AABB(minX, minY, minX + sizeX, minY + sizeY);

        /// <summary>
        /// Creates a body box from a lower-center ground anchor and a body size. The horizontal extent is
        /// centered on the anchor and the box rests on the anchor's height, which is the body pose that
        /// ground-anchored capabilities plan and execute with.
        /// </summary>
        /// <param name="lowerCenter"></param>
        /// <param name="bodySize"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromLowerCenter(Vector2 lowerCenter, Vector2 bodySize)
            => FromMinAndSize(new Vector2(lowerCenter.x - bodySize.x * 0.5f, lowerCenter.y), bodySize);

        /// <summary>
        /// Creates degenerate goal geometry for one continuous world-space point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Point(Vector2 point) => new(point, point);

        /// <summary>
        /// Creates degenerate goal geometry for one continuous world-space point given as scalars.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB Point(float x, float y) => new(x, y, x, y);

        /// <summary>Maps Unity's floating rectangle onto this box without validating the range.</summary>
        public static AABB FromRect(Rect rect) => new(rect.min, rect.max);

        /// <summary>Maps this box onto Unity's floating rectangle without validating the range.</summary>
        public readonly Rect ToRect() => new(Min, Size);

        /// <summary>Maps Unity's floating bounds onto this box without validating the range.</summary>
        public static AABB FromBounds(Bounds bounds) => new(bounds.min, bounds.max);

        /// <summary>Maps this box onto Unity's floating bounds without validating the range.</summary>
        public readonly Bounds ToBounds() => new(Center, Size);

        public static bool operator ==(AABB left, AABB right) => left.Equals(right);
        public static bool operator !=(AABB left, AABB right) => !left.Equals(right);
    }
}
