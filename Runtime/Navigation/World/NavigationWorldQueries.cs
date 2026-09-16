using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Small coordinate and compatibility helpers around the world-space contract.</summary>
    public static class NavigationWorldQueries
    {
        public const float GeometryEpsilon = NavigationConstant.Epsilon;
        public static float SupportSnapDistance => NavigationConstant.SupportSnap;

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

        public static bool IsLowerCenterBodyClearAt(this INavigationWorld world, Vector2 lowerCenterPosition, Vector2 bodySize)
            => world.IsBodyClear(new Rect(lowerCenterPosition.x - bodySize.x * 0.5f, lowerCenterPosition.y, bodySize.x, bodySize.y), 0f);

        public static bool IsCenteredBodyClearAt(this INavigationWorld world, Vector2 centerPosition, Vector2 bodySize)
            => world.IsBodyClear(new Rect(centerPosition - bodySize * 0.5f, bodySize), 0f);

        public static bool CanStandAt(this INavigationWorld world, Vector2 lowerCenterPosition, Vector2 bodySize, out bool supportIsOneWay)
            => world.CanStandAt(lowerCenterPosition, bodySize, SupportSnapDistance, out supportIsOneWay);

        public static bool CanStandAt(this INavigationWorld world, Vector2 lowerCenterPosition, Vector2 bodySize, float supportSnapDistance, out bool supportIsOneWay)
        {
            bool resolved = world.TryResolveSupport(lowerCenterPosition, bodySize, supportSnapDistance, out NavigationSupport support);
            supportIsOneWay = resolved && support.Kind == NavigationSurfaceKind.OneWay;
            return resolved;
        }

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter, Vector2 bodySize, out Vector2 snappedLowerCenter, out NavigationSupport support)
            => TryResolveGroundSupport(world, observedLowerCenter, bodySize, SupportSnapDistance, out snappedLowerCenter, out support);

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter, Vector2 bodySize, float supportSnapDistance, out Vector2 snappedLowerCenter, out NavigationSupport support)
        {
            bool resolved = world.TryResolveSupport(observedLowerCenter, bodySize, supportSnapDistance, out support);
            snappedLowerCenter = resolved ? support.Position : default;
            return resolved;
        }

        public static bool IsLowerCenterSegmentClear(this INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize)
            => world.IsBodyPathClear(new Rect(start.x - bodySize.x * 0.5f, start.y, bodySize.x, bodySize.y), end - start, 0f);

        public static bool IsLowerCenterSegmentClear(this INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize, float surfaceContactTolerance)
            => world.IsBodyPathClear(new Rect(start.x - bodySize.x * 0.5f, start.y, bodySize.x, bodySize.y), end - start, surfaceContactTolerance);

        public static bool IsCenteredBodySegmentClear(this INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize)
            => world.IsBodyPathClear(new Rect(start - bodySize * 0.5f, bodySize), end - start, 0f);

        public static bool CrossesOneWayDown(this INavigationWorld world, Vector2 previousFeet, Vector2 currentFeet, float bodyWidth, float ignoredSurfaceY = float.NaN)
        {
            List<NavigationSurfaceCrossing> crossings = new();
            world.CollectOneWayCrossings(previousFeet, currentFeet, bodyWidth, crossings);
            if (float.IsNaN(ignoredSurfaceY)) return crossings.Count > 0;
            for (int index = 0; index < crossings.Count; index++)
                if (Mathf.Abs(crossings[index].Position.y - ignoredSurfaceY) > GeometryEpsilon) return true;
            return false;
        }
    }
}
