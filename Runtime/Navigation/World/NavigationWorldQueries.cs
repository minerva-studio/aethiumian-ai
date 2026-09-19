using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Small coordinate and Unity-side helpers around the world-space contract. Every helper here takes
    /// a body AABB, so the conversion from a body pose to the lower-center anchor and size the world
    /// stores has exactly one owner instead of being repeated by each caller.
    /// </summary>
    public static class NavigationWorldQueries
    {
        public const float GeometryEpsilon = NavigationConstant.Epsilon;
        private static float supportSnapDistance;
        private static int supportSnapDistanceCaptured;

        /// <summary>
        /// Gets the standard support distance captured from the Unity physics policy.
        /// The cached managed value keeps background planning from reading Unity physics state.
        /// </summary>
        public static float SupportSnapDistance
        {
            get
            {
                CaptureSupportSnapDistance();
                return supportSnapDistance;
            }
        }

        /// <summary>Captures Unity physics policy on the main-thread owner boundary.</summary>
        internal static void CaptureSupportSnapDistance()
        {
            if (Volatile.Read(ref supportSnapDistanceCaptured) != 0) return;
            supportSnapDistance = NavigationConstant.SupportSnap;
            Volatile.Write(ref supportSnapDistanceCaptured, 1);
        }

        /// <summary>Finds physical support among candidates selected by the caller's terrain filter.</summary>
        public static bool TryGetGroundSupportPoint(Collider2D bodyCollider, ContactFilter2D supportFilter, out Vector2 supportPoint)
        {
            if (!bodyCollider) throw new ArgumentNullException(nameof(bodyCollider));
            RaycastHit2D[] hits = new RaycastHit2D[8];
            int count = bodyCollider.Cast(Vector2.down, supportFilter, hits, SupportSnapDistance);
            float nearestDistance = float.PositiveInfinity;
            supportPoint = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                if (!hit.collider || hit.collider == bodyCollider || Physics2D.GetIgnoreCollision(bodyCollider, hit.collider)
                    || hit.normal.y <= GeometryEpsilon || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                supportPoint = hit.point;
            }
            return nearestDistance < float.PositiveInfinity;
        }

        /// <summary>Returns whether the body may stand where it is, using the standard support snap distance.</summary>
        public static bool CanStandAt(this INavigationWorld world, AABB body, out bool supportIsOneWay)
            => world.CanStandAt(body, SupportSnapDistance, out supportIsOneWay);

        /// <summary>Returns whether the body may stand where it is with an explicit support snap distance.</summary>
        public static bool CanStandAt(this INavigationWorld world, AABB body, float supportSnapDistance, out bool supportIsOneWay)
        {
            bool resolved = world.TryResolveSupport(body, supportSnapDistance, out NavigationSupport support);
            supportIsOneWay = resolved && support.Kind == NavigationSurfaceKind.OneWay;
            return resolved;
        }

        /// <summary>Resolves the support under a body, using the standard support snap distance.</summary>
        public static bool TryResolveGroundSupport(this INavigationWorld world, AABB body, out Vector2 snappedLowerCenter, out NavigationSupport support)
            => TryResolveGroundSupport(world, body, SupportSnapDistance, out snappedLowerCenter, out support);

        /// <summary>Resolves the support under a body with an explicit support snap distance.</summary>
        public static bool TryResolveGroundSupport(this INavigationWorld world, AABB body, float supportSnapDistance, out Vector2 snappedLowerCenter, out NavigationSupport support)
        {
            bool resolved = world.TryResolveSupport(body, supportSnapDistance, out support);
            snappedLowerCenter = resolved ? support.Position : default;
            return resolved;
        }

        /// <summary>Returns whether one swept body crosses an allowed one-way surface downward.</summary>
        public static bool CrossesOneWayDown(this INavigationWorld world, AABB previousBody, Vector2 displacement, float ignoredSurfaceY = float.NaN)
        {
            List<NavigationSurfaceCrossing> crossings = new();
            world.CollectOneWayCrossings(previousBody, displacement, crossings);
            if (float.IsNaN(ignoredSurfaceY)) return crossings.Count > 0;
            for (int index = 0; index < crossings.Count; index++)
                if (Mathf.Abs(crossings[index].Position.y - ignoredSurfaceY) > GeometryEpsilon) return true;
            return false;
        }
    }
}
