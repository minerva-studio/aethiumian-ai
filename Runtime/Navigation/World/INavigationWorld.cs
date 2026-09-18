using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Provides immutable world-space navigation queries. Implementations must not retain Unity objects,
    /// access Physics2D, or mutate captured geometry while planner threads are reading it. Implementations
    /// may maintain private, thread-safe, bounded derived-computation caches.
    /// </summary>
    public interface INavigationWorld
    {
        /// <summary>
        /// Continuous world-space box of the captured finite world. Geometry outside it is never
        /// navigable; discrete half-open cell ranges belong to <see cref="AABBInt"/> instead.
        /// </summary>
        AABB WorldBounds { get; }

        /// <summary>
        /// Checks body clearance against captured geometry with the supplied contact tolerance.
        /// </summary>
        bool IsBodyClear(AABB body, float surfaceContactTolerance);

        /// <summary>
        /// Checks clearance along a body's displacement through captured geometry.
        /// </summary>
        bool IsBodyPathClear(AABB startBody, Vector2 displacement, float surfaceContactTolerance);

        /// <summary>
        /// Checks whether captured geometry permits line of sight between two positions.
        /// </summary>
        bool IsLineOfSightClear(Vector2 start, Vector2 end);

        /// <summary>
        /// Resolves physical support for a body. A valid support may contact any overlapping part of the
        /// body's foot interval; it is not limited to a center-ray hit. Returned anchors retain the
        /// supplied body's horizontal center.
        /// </summary>
        bool TryResolveSupport(AABB body, float snapDistance, out NavigationSupport support);

        /// <summary>
        /// Finds the nearest upward-facing support at the point's X, at or below its Y within
        /// geometry tolerance, down to the captured world's lower boundary. Includes allowed
        /// one-way surfaces. This point query does not validate standing body clearance.
        /// Returns false when no support exists; it never invents a zero-height surface.
        /// </summary>
        bool TryGetSupportBelow(Vector2 position, out NavigationSupport support);

        /// <summary>
        /// Returns read-only support candidates for the requested anchor bounds and body size.
        /// Results remain valid after cache eviction and do not contain goal-specific filtering.
        /// </summary>
        IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(AABB anchorBounds, Vector2 bodySize);

        /// <summary>
        /// Appends support candidates to the supplied list without clearing existing entries.
        /// </summary>
        void CollectSupportCandidates(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results);

        /// <summary>
        /// Collects directed one-way surface crossings along one swept body. The displacement is separate
        /// from the body geometry, exactly as it is for <see cref="IsBodyPathClear"/>.
        /// </summary>
        void CollectOneWayCrossings(AABB previousBody, Vector2 displacement, List<NavigationSurfaceCrossing> results);

        /// <summary>
        /// Checks whether two positions belong to the same captured navigation region.
        /// </summary>
        bool AreInSameRegion(Vector2 first, Vector2 second);
    }
}
