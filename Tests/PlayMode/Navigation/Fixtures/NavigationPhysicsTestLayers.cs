using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Defines package-local Physics2D layers for isolated navigation fixtures.
    /// </summary>
    internal static class NavigationPhysicsTestLayers
    {
        internal const int DefaultLayer = 0;
        internal const int GeometryLayer = 1;
        internal const int PlatformLayer = 2;
        internal const int DecorationLayer = 3;

        internal const int DefaultMask = 1 << DefaultLayer;
        internal const int GeometryMask = 1 << GeometryLayer;
        internal const int PlatformMask = 1 << PlatformLayer;
        internal const int TerrainMask = GeometryMask | PlatformMask;

        /// <summary>
        /// Creates the execution filter shared by package-owned terrain fixtures.
        /// </summary>
        internal static ContactFilter2D CreateTerrainFilter() => new NavigationPhysicsLayers(GeometryMask, PlatformMask).CreateTerrainFilter();
    }
}
