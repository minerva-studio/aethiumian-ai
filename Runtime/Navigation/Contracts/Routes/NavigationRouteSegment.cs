using System;
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

        /// <summary>
        /// Gets whether the movement still owns this segment after starting it. A reversible segment
        /// may be abandoned, replaced, or ended mid-action; an irreversible one runs until the physics
        /// it started has finished. This is a commitment contract, not a direction contract: it says
        /// nothing about traversing the segment backwards. The default is false so an unknown segment
        /// type is never abandoned in mid-physics.
        /// </summary>
        public virtual bool IsReversible => false;

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
}
