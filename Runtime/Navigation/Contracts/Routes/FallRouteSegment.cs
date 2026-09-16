using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Represents a horizontal ledge exit followed by a downward fall.</summary>
    public sealed class FallRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => false;

        /// <summary>Gets the validated world-space waypoint where downward falling begins.</summary>
        public Vector2 LedgeExit { get; }

        /// <summary>Creates a fall route segment with its ledge-exit waypoint.</summary>
        public FallRouteSegment(Vector2 start, Vector2 ledgeExit, Vector2 end) : base(start, end)
        {
            if (!NavigationNumeric.IsFinite(ledgeExit))
            {
                throw new ArgumentException("Fall ledge exit must be a finite world coordinate.", nameof(ledgeExit));
            }

            if (!Mathf.Approximately(ledgeExit.y, start.y) || end.y >= ledgeExit.y)
            {
                throw new ArgumentException("Fall ledge exit must be horizontally aligned with the start and above the end.", nameof(ledgeExit));
            }

            LedgeExit = ledgeExit;
        }
    }
}
