using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Single owner for every navigation number: spatial-index resolution, support-anchor spacing,
    /// sampling steps, search-lattice steps, and the error tolerances the stack reasons about. Every
    /// value is named after the quantity a call site actually needs, so no site has to borrow an
    /// unrelated constant. Call sites inside Runtime/Navigation must not introduce numeric literals of
    /// their own; add the concept here and document its derivation.
    /// </summary>
    public static class NavigationConstant
    {
        // Spatial discretization. These describe how the implementation indexes and samples continuous
        // geometry; they are not properties of the world and never appear in a public query signature.

        /// <summary>
        /// Uniform bucket size of the snapshot's spatial index. Pure lookup partitioning: it changes how
        /// many shapes one query scans, never a query result.
        /// </summary>
        internal const float SpatialIndexBucketSize = 1f;

        /// <summary>
        /// Horizontal spacing at which a support surface contributes search anchors. This is the
        /// granularity of grounded search nodes and of accepted jump landing positions; it must stay at
        /// or below <see cref="GroundHopReach"/> or a flat floor loses its successors. Support itself is
        /// resolved exactly at any X, so this bounds search nodes only.
        /// </summary>
        internal const float SupportAnchorSpacing = 1f;

        // Sampling steps: how finely a query subdivides the geometry it validates.

        /// <summary>
        /// Upper bound for the spacing at which a grounded move re-validates support and clearance.
        /// </summary>
        internal const float MaximumSupportSampleSpacing = 0.25f;

        /// <summary>
        /// Upper bound for the spacing at which a horizontal traversal is re-validated.
        /// </summary>
        internal const float MaximumTraversalSampleSpacing = 0.2f;

        /// <summary>
        /// Upper bound for the spacing at which a vertical drop or fall is re-validated.
        /// </summary>
        internal const float MaximumVerticalValidationSpacing = 0.15f;

        /// <summary>
        /// Spacing at which a swept body samples captured geometry when testing clearance.
        /// </summary>
        internal const float BodySweepSampleSpacing = 0.25f;

        /// <summary>
        /// Spacing at which a swept body samples oriented one-way surfaces for crossings.
        /// </summary>
        internal const float OneWayCrossingSampleSpacing = 0.2f;

        /// <summary>
        /// Spacing at which a goal sweep samples positions for the optional line-of-sight constraint.
        /// </summary>
        internal const float LineOfSightSweepSpacing = 0.5f;

        // Search lattices. Each planner owns its own discretization; none of them is shared with the world index.

        /// <summary>
        /// Step of the aerial search lattice, owned by the fly planner alone.
        /// </summary>
        internal const float FlightStep = 1f;

        /// <summary>
        /// Half-width of the neighbourhood a grounded search expands in one hop.
        /// </summary>
        internal const float GroundHopReach = 1.5f;

        // Physical thresholds, named after the physical quantity each one bounds.

        /// <summary>
        /// Clearance a jump must gain above the landing support before its descent.
        /// </summary>
        internal const float LandingApexClearance = 1f;

        /// <summary>
        /// Horizontal distance at which a same-level landing counts as walkable rather than a jump
        /// target. Deliberately unrelated to any index resolution.
        /// </summary>
        internal const float AdjacentSupportReach = 1f;

        /// <summary>
        /// Smallest displacement a single retreat step must produce.
        /// </summary>
        internal const float MinimumRetreatStep = 1f;

        /// <summary>
        /// Distance a live target may drift before a pending plan belongs to a different target. This is
        /// a re-planning policy, deliberately unrelated to any index resolution and to a goal's authored
        /// arrival tolerance.
        /// </summary>
        internal const float TargetMotionTolerance = 1f;

        // Error tolerances.

        /// <summary>
        /// Contact offset Unity keeps between two resting colliders. Read from physics, not authored.
        /// </summary>
        public static float ContactGap => Physics2D.defaultContactOffset;

        /// <summary>
        /// How far a probed anchor may sit from the support surface and still resolve to it. Covers the
        /// contact gap on both sides of a resting contact.
        /// </summary>
        public static float SupportSnap => 2f * ContactGap + Epsilon;

        /// <summary>
        /// Vertical slack of a resolved support position. This compares support to support; it is not a
        /// licence to compare a physical body anchor against an authored support point, which are
        /// different quantities and differ by one contact gap by construction.
        /// </summary>
        public static float SupportResidual => ContactGap + Epsilon;

        /// <summary>
        /// Guard for float noise in navigation comparisons.
        /// </summary>
        public const float Epsilon = 0.0001f;

        /// <summary>
        /// Finer guard for iterative solvers that accumulate more rounding.
        /// </summary>
        public const float SolverEpsilon = 0.000001f;

        /// <summary>
        /// Smallest horizontal tolerance for a completed traversal, independent of speed.
        /// </summary>
        public const float ArrivalFloor = 0.2f;

        /// <summary>
        /// Axis length or component below which a direction is treated as degenerate.
        /// </summary>
        public const float DegenerateAxis = 0.0000001f;

        /// <summary>
        /// How far ahead an executor probes for terrain while moving.
        /// </summary>
        public const float GroundProbeDistance = 0.08f;

        /// <summary>
        /// Smallest horizontal velocity an executor is allowed to write without stalling.
        /// </summary>
        public const float MinimumMotion = 0.001f;

        /// <summary>
        /// Opposing normal component required before a sweep hit counts as an obstacle rather than as
        /// ground the body is travelling along. Geometry, not motion.
        /// </summary>
        public const float ObstacleNormalThreshold = 0.0001f;
    }
}
