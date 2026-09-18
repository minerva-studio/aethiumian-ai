using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Represents a temporary drop-through traversal between two ground-anchor world positions.</summary>
    public sealed class DropThroughRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => false;

        public override NavigationRouteCoordinateFrame CoordinateFrame => NavigationRouteCoordinateFrame.GroundAnchor;

        /// <summary>Creates a drop-through route segment.</summary>
        public DropThroughRouteSegment(Vector2 start, Vector2 end) : base(start, end) { }
    }

}
