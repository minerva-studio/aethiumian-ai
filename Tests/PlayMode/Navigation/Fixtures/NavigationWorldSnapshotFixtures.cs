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
        private static readonly RectInt DefaultBounds = new(-100, -100, 200, 200);

        public static NavigationWorldSnapshot Open()
            => NavigationWorldSnapshot.Create(
                Vector2.zero,
                1f,
                DefaultBounds,
                Array.Empty<NavigationShapeData>(),
                new[] { new NavigationRegionData(DefaultBounds, 0) });

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
                Vector2.zero,
                1f,
                DefaultBounds,
                new[] { ground },
                new[] { new NavigationRegionData(DefaultBounds, 0) });
        }
    }
}
