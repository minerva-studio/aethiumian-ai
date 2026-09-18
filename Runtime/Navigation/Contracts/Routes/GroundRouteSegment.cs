using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Represents ordinary ground movement between two ground-anchor world positions.</summary>
    public sealed class GroundRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => true;

        public override NavigationRouteCoordinateFrame CoordinateFrame => NavigationRouteCoordinateFrame.GroundAnchor;

        /// <summary>Creates a ground route segment.</summary>
        public GroundRouteSegment(Vector2 start, Vector2 end) : base(start, end) { }
    }

}
