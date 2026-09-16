using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Represents aerial movement between two world positions.</summary>
    public sealed class FlyRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => true;

        /// <summary>Creates a fly route segment.</summary>
        public FlyRouteSegment(Vector2 start, Vector2 end) : base(start, end) { }
    }

}
