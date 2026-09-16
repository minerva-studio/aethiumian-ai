using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Single owner for the built-in error tolerances the navigation stack reasons about. Every value
    /// is derived from a named physical quantity so a call site can ask for the quantity it actually
    /// needs instead of borrowing an unrelated constant. Call sites inside Runtime/Navigation must not
    /// introduce tolerance literals of their own; add the concept here and document its derivation.
    /// </summary>
    public static class NavigationTolerances
    {
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
