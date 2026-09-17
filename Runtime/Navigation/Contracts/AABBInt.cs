using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Discrete axis-aligned box on the navigation lattice, covering the half-open cell range
    /// [Min, Max). It validates nothing and leaves range checking to its author.
    /// </summary>
    public struct AABBInt : IEquatable<AABBInt>
    {
        /// <summary>The inclusive lower cell corner X.</summary>
        public int MinX { get; set; }

        /// <summary>The inclusive lower cell corner Y.</summary>
        public int MinY { get; set; }

        /// <summary>The exclusive upper cell corner X.</summary>
        public int MaxX { get; set; }

        /// <summary>The exclusive upper cell corner Y.</summary>
        public int MaxY { get; set; }


        /// <summary>Gets or sets the inclusive lower cell corner. The components are the scalar storage above.</summary>
        public Vector2Int Min
        {
            readonly get => new(MinX, MinY);
            set { MinX = value.x; MinY = value.y; }
        }

        /// <summary>Gets or sets the exclusive upper cell corner. The components are the scalar storage above.</summary>
        public Vector2Int Max
        {
            readonly get => new(MaxX, MaxY);
            set { MaxX = value.x; MaxY = value.y; }
        }

        public readonly int SizeX => MaxX - MinX;
        public readonly int SizeY => MaxY - MinY;
        public readonly Vector2Int Size => new(MaxX - MinX, MaxY - MinY);
        public readonly Vector2Int Center => new(MinX + (MaxX - MinX) / 2, MinY + (MaxY - MinY) / 2);
        public readonly Vector2 FloatCenter => new(MinX + (MaxX - MinX) * 0.5f, MinY + (MaxY - MinY) * 0.5f);


        public AABBInt(int minX, int minY, int maxExclusiveX, int maxExclusiveY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxExclusiveX;
            MaxY = maxExclusiveY;
        }

        public AABBInt(Vector2Int min, Vector2Int maxExclusive) : this(min.x, min.y, maxExclusive.x, maxExclusive.y) { }

        /// <summary>Returns whether a cell lies inside the half-open range.</summary>
        public readonly bool Contains(Vector2Int cell)
            => cell.x >= MinX && cell.x < MaxX && cell.y >= MinY && cell.y < MaxY;

        public override readonly string ToString() => $"AABBInt(Min: {Min}, Max: {Max})";

        public readonly bool Equals(AABBInt other)
            => MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;

        public readonly override bool Equals(object obj) => obj is AABBInt other && Equals(other);
        public readonly override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);

        public static bool operator ==(AABBInt left, AABBInt right) => left.Equals(right);
        public static bool operator !=(AABBInt left, AABBInt right) => !left.Equals(right);



        /// <summary>Maps Unity's integer rectangle onto this range without validating it.</summary>
        public static AABBInt FromRectInt(RectInt rect) => new(rect.min, rect.max);

        /// <summary>Maps this half-open range onto Unity's integer rectangle without validating it.</summary>
        public readonly RectInt ToRectInt() => new(Min, Size);
    }
}
