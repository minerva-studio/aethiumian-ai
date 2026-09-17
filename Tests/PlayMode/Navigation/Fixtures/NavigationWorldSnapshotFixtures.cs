using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Provides the shared package PlayMode world snapshots used by planner-result
    /// handoff tests.  Keeping the authored bounds, region and support geometry in
    /// one fixture prevents each test family from inventing a subtly different world.
    /// </summary>
    internal static class NavigationWorldSnapshotFixtures
    {
        private static readonly AABB DefaultBounds = AABB.FromMinAndSize(-100f, -100f, 200f, 200f);

        public static NavigationWorldSnapshot Open()
            => NavigationWorldSnapshot.Create(
                DefaultBounds,
                Array.Empty<NavigationShapeData>(),
                new[] { new NavigationRegionData(DefaultBounds, 0) });

        /// <summary>
        /// Publishes a solid floor. Unlike the one-way floor this surface needs no
        /// PlatformEffector2D source binding, so a jump across it creates no platform lease.
        /// </summary>
        public static NavigationWorldSnapshot SolidGround(float y = 0f, float thickness = 0.5f)
        {
            NavigationShapeData floor = new(
                1,
                0,
                NavigationShapeType.Polygon,
                new[]
                {
                    new Vector2(-20f, y - thickness),
                    new Vector2(100f, y - thickness),
                    new Vector2(100f, y),
                    new Vector2(-20f, y),
                },
                0f,
                NavigationSurfaceKind.Solid,
                true);
            return NavigationWorldSnapshot.Create(
                DefaultBounds,
                new[] { floor },
                new[] { new NavigationRegionData(DefaultBounds, 0) });
        }

        public static NavigationWorldSnapshot Ground(float y = 0f)
        {
            NavigationShapeData ground = new(
                1,
                0,
                NavigationShapeType.Edge,
                new[] { new Vector2(-20f, y), new Vector2(100f, y) },
                0f,
                NavigationSurfaceKind.OneWay,
                true,
                Vector2.up);
            return NavigationWorldSnapshot.Create(
                DefaultBounds,
                new[] { ground },
                new[] { new NavigationRegionData(DefaultBounds, 0) });
        }
    }
}
