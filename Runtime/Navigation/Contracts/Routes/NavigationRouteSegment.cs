using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Identifies the coordinate frame shared by every position in one navigation route. Ground and
    /// jump/fall/drop-through traversal speaks in ground anchors; aerial traversal speaks in body
    /// centers. A route may never mix two frames.
    /// </summary>
    public enum NavigationRouteCoordinateFrame
    {
        /// <summary>Positions are lower-center ground anchors, the point a grounded body rests on.</summary>
        GroundAnchor = 0,
        /// <summary>Positions are body centers.</summary>
        BodyCenter = 1,
    }

    /// <summary>
    /// Immutable geometry for one traversal edge. A segment describes movement
    /// boundaries and capability only; it is not a physical command or action queue.
    /// Every position it exposes uses its declared <see cref="CoordinateFrame"/>.
    /// </summary>
    public abstract class NavigationRouteSegment
    {
        /// <summary>
        /// Gets the world-space start position of this segment, in <see cref="CoordinateFrame"/>.
        /// </summary>
        public Vector2 Start { get; }

        /// <summary>
        /// Gets the world-space end position of this segment, in <see cref="CoordinateFrame"/>.
        /// </summary>
        public Vector2 End { get; }

        /// <summary>
        /// Gets whether the movement still owns this segment after starting it. A reversible segment
        /// may be abandoned, replaced, or ended mid-action; an irreversible one runs until the physics
        /// it started has finished. This is a commitment contract, not a direction contract: it says
        /// nothing about traversing the segment backwards. The default is false so an unknown segment
        /// type is never abandoned in mid-physics.
        /// </summary>
        public virtual bool IsReversible => false;

        /// <summary>
        /// Gets the coordinate frame of <see cref="Start"/> and <see cref="End"/>. Each segment family
        /// declares its own frame, and that declaration is what lets route consumers convert between
        /// ground anchors and body centers instead of guessing from the segment type.
        /// </summary>
        public abstract NavigationRouteCoordinateFrame CoordinateFrame { get; }

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
