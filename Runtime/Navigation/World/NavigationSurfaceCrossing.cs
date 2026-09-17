using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// One one-way surface crossing along a trajectory segment.
    /// </summary>
    public readonly struct NavigationSurfaceCrossing
    {
        public NavigationSurfaceId Surface { get; }
        public Vector2 Position { get; }
        public Vector2 Normal { get; }
        public float Fraction { get; }

        public NavigationSurfaceCrossing(NavigationSurfaceId surface, Vector2 position, Vector2 normal, float fraction)
        {
            if (!NavigationNumeric.IsFinite(position) || !NavigationNumeric.IsFinite(normal) || !NavigationNumeric.IsFinite(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentException("Navigation crossing must be finite and normalized.");
            Surface = surface;
            Position = position;
            Normal = normal;
            Fraction = fraction;
        }
    }
}
