using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Represents a temporary drop-through traversal between two world positions.</summary>
    public sealed class DropThroughRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => false;

        /// <summary>Creates a drop-through route segment.</summary>
        public DropThroughRouteSegment(Vector2 start, Vector2 end) : base(start, end) { }
    }

}
