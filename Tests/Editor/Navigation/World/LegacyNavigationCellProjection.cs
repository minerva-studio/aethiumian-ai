using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Legacy occupancy projection retained only while Explicit tests still require cell diagnostics.</summary>
    [System.Obsolete("Legacy test-only occupancy projection. Prefer world-space support and clearance contracts.", false)]
    internal enum NavigationCell { Empty, OneWay, Solid }

    /// <summary>Adapts world-space navigation queries for legacy occupancy assertions during migration.</summary>
    [System.Obsolete("Legacy test-only occupancy projection. Prefer world-space support and clearance contracts.", false)]
    internal static class NavigationTestWorldExtensions
    {
        public static NavigationCell GetCell(this INavigationWorld world, Vector2Int cell)
        {
            if (world == null || !world.CellBounds.Contains(cell)) return NavigationCell.Solid;
            Vector2 min = world.Origin + Vector2.Scale(cell, Vector2.one) * world.CellSize;
            Rect interior = new(min.x + world.CellSize * 0.1f, min.y + world.CellSize * 0.1f,
                world.CellSize * 0.8f, world.CellSize * 0.8f);
            if (!world.IsBodyClear(interior, 0f)) return NavigationCell.Solid;
            return world.TryResolveSupport(
                new Vector2(min.x + world.CellSize * 0.5f, min.y + world.CellSize),
                new Vector2(world.CellSize * 0.1f, world.CellSize * 0.1f),
                world.CellSize * 0.25f,
                out NavigationSupport support)
                && support.Kind == NavigationSurfaceKind.OneWay
                    ? NavigationCell.OneWay
                    : NavigationCell.Empty;
        }

        public static bool TryGetSupportSurfaceY(this INavigationWorld world, Vector2Int cell, out float surfaceY)
        {
            surfaceY = default;
            if (world == null || !world.CellBounds.Contains(cell)) return false;
            Vector2 min = world.Origin + Vector2.Scale(cell, Vector2.one) * world.CellSize;
            return world.TryResolveSupport(
                new Vector2(min.x + world.CellSize * 0.5f, min.y + world.CellSize),
                new Vector2(world.CellSize * 0.1f, world.CellSize * 0.1f),
                world.CellSize * 0.25f,
                out NavigationSupport support)
                && (surfaceY = support.Position.y) == support.Position.y;
        }

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter,
            Vector2 bodySize, float snapDistance, out Vector2 snappedLowerCenter,
            out Vector2Int supportCell, out NavigationCell supportKind)
        {
            snappedLowerCenter = default;
            supportCell = default;
            supportKind = NavigationCell.Empty;
            if (!world.TryResolveSupport(observedLowerCenter, bodySize, snapDistance,
                out NavigationSupport support)) return false;
            snappedLowerCenter = support.Position;
            supportCell = NavigationWorldQueries.WorldToCell(world, support.Position);
            supportKind = support.Kind == NavigationSurfaceKind.OneWay
                ? NavigationCell.OneWay : NavigationCell.Solid;
            return true;
        }

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter,
            Vector2 bodySize, out Vector2 snappedLowerCenter, out Vector2Int supportCell,
            out NavigationCell supportKind)
            => TryResolveGroundSupport(world, observedLowerCenter, bodySize,
                NavigationWorldQueries.SupportSnapDistance, out snappedLowerCenter, out supportCell, out supportKind);
    }
}
