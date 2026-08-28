using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Describes the occupancy and support state of one immutable navigation cell.</summary>
    public enum NavigationCell : byte
    {
        /// <summary>The cell contains no navigation geometry.</summary>
        Empty,

        /// <summary>The cell contributes an upward-facing one-way surface.</summary>
        OneWay,

        /// <summary>The cell is occupied by blocking geometry.</summary>
        Solid,
    }

    /// <summary>
    /// Provides a permanently immutable navigation snapshot that supports concurrent planner reads.
    /// Implementations must keep their published storage valid and unreused for the snapshot lifetime.
    /// Queries must not access Unity scene objects, Physics2D, engine-owned mutable state, or main-thread-only state.
    /// </summary>
    public interface INavigationWorld
    {
        /// <summary>Gets the world-space origin of cell coordinate zero.</summary>
        Vector2 Origin { get; }

        /// <summary>Gets the world-space edge length of each square cell.</summary>
        float CellSize { get; }

        /// <summary>Gets the finite range of valid cell indices.</summary>
        RectInt CellBounds { get; }

        /// <summary>Gets the occupancy state of a cell; cells outside <see cref="CellBounds"/> are solid.</summary>
        NavigationCell GetCell(Vector2Int cell);
    }
}
