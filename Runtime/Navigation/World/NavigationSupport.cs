using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable body support result. Position is the body's lower-center anchor, whose x coordinate
    /// remains the queried body center even when only part of the feet overlap the supporting surface.
    /// </summary>
    public readonly struct NavigationSupport : IComparable<NavigationSupport>
    {
        public NavigationSurfaceId Surface { get; }
        public NavigationSurfaceKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2 Normal { get; }

        public NavigationSupport(NavigationSurfaceId surface, NavigationSurfaceKind kind, Vector2 position, Vector2 normal)
        {
            if (!NavigationNumeric.IsFinite(position) || !NavigationNumeric.IsFinite(normal)) throw new ArgumentException("Navigation support must be finite.");
            Surface = surface;
            Kind = kind;
            Position = position;
            Normal = normal;
        }

        public int CompareTo(NavigationSupport other)
        {
            int surface = Surface.CompareTo(other.Surface);
            if (surface != 0) return surface;
            int x = Position.x.CompareTo(other.Position.x);
            return x != 0 ? x : Position.y.CompareTo(other.Position.y);
        }
    }
}
