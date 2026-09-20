using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Identifies the phase in which a planned OneWay surface is crossed.</summary>
    public enum JumpSurfaceCrossingKind
    {
        Ascending,
        Descending,
        Landing,
    }

    /// <summary>Describes one real directed OneWay surface crossing on a planned trajectory.</summary>
    public readonly struct JumpSurfaceCrossing
    {
        public NavigationSurfaceId Surface { get; }
        public Vector2 Position { get; }
        public Vector2 Normal { get; }
        public float Fraction { get; }

        /// <summary>Gets the crossing phase represented by this span.</summary>
        public JumpSurfaceCrossingKind Kind { get; }

        /// <summary>Creates one immutable crossing with its route phase.</summary>
        public JumpSurfaceCrossing(NavigationSurfaceId surface, Vector2 position, Vector2 normal,
            float fraction, JumpSurfaceCrossingKind kind)
        {
            if (!NavigationNumeric.IsFinite(position) || !NavigationNumeric.IsFinite(normal) || !NavigationNumeric.IsFinite(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentException("Jump surface crossing must be finite and normalized.");
            Surface = surface;
            Position = position;
            Normal = normal;
            Fraction = fraction;
            Kind = kind;
        }

    }

    /// <summary>Represents a jump launch and planned landing between two ground-anchor world positions.</summary>
    public sealed class JumpRouteSegment : NavigationRouteSegment
    {
        public override bool IsReversible => false;

        public override NavigationRouteCoordinateFrame CoordinateFrame => NavigationRouteCoordinateFrame.GroundAnchor;

        /// <summary>Gets the minimum apex displacement selected by collision-aware planning.</summary>
        public float MinimumApexHeight { get; }

        /// <summary>Gets the shared OneWay surface spans, which must remain unchanged for the route lifetime.</summary>
        public IReadOnlyList<JumpSurfaceCrossing> SurfaceCrossings { get; }

        /// <summary>Creates a route-only jump segment.</summary>
        public JumpRouteSegment(Vector2 launchSupport, Vector2 plannedLanding)
            : this(launchSupport, plannedLanding, 0f) { }

        /// <summary>Creates a route-only jump segment with its selected minimum apex.</summary>
        public JumpRouteSegment(Vector2 launchSupport, Vector2 plannedLanding, float minimumApexHeight)
            : this(launchSupport, plannedLanding, minimumApexHeight, null) { }

        /// <summary>Creates a route-only jump segment with its selected apex and surface spans.</summary>
        /// <remarks>The supplied collection is retained without copying. Its owner must not modify,
        /// clear, pool, or reuse it while the route is alive. Snapshot reusable buffers before passing them.</remarks>
        public JumpRouteSegment(Vector2 launchSupport, Vector2 plannedLanding, float minimumApexHeight,
            IReadOnlyList<JumpSurfaceCrossing> surfaceCrossings)
            : base(launchSupport, plannedLanding)
        {
            if (!NavigationNumeric.IsFinite(minimumApexHeight) || minimumApexHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumApexHeight));
            MinimumApexHeight = minimumApexHeight;
            SurfaceCrossings = surfaceCrossings ?? Array.Empty<JumpSurfaceCrossing>();
        }
    }

}
