using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable geometry for one traversal edge. A segment describes movement
    /// boundaries and capability only; it is not a physical command or action queue.
    /// </summary>
    public abstract class NavigationRouteSegment
    {
        /// <summary>Gets the world-space start position of this segment.</summary>
        public Vector2 Start { get; }

        /// <summary>Gets the world-space end position of this segment.</summary>
        public Vector2 End { get; }

        /// <summary>Creates a finite immutable segment boundary.</summary>
        protected NavigationRouteSegment(Vector2 start, Vector2 end)
        {
            if (!NavigationNumeric.IsFinite(start) || !NavigationNumeric.IsFinite(end))
            {
                throw new ArgumentException("Route segment positions must be finite world coordinates.");
            }

            Start = start;
            End = end;
        }

    }

    /// <summary>Represents ordinary ground movement between two world positions.</summary>
    public sealed class GroundRouteSegment : NavigationRouteSegment
    {
        /// <summary>Creates a ground route segment.</summary>
        public GroundRouteSegment(Vector2 start, Vector2 end) : base(start, end) { }
    }

}
