using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Identifies a captured navigation surface within one immutable snapshot.</summary>
    public readonly struct NavigationSurfaceId : IEquatable<NavigationSurfaceId>
    {
        public int SourceId { get; }
        public int FeatureId { get; }

        public NavigationSurfaceId(int sourceId, int featureId)
        {
            if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (featureId < 0) throw new ArgumentOutOfRangeException(nameof(featureId));
            SourceId = sourceId;
            FeatureId = featureId;
        }

        public bool Equals(NavigationSurfaceId other) => SourceId == other.SourceId && FeatureId == other.FeatureId;
        public override bool Equals(object obj) => obj is NavigationSurfaceId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceId, FeatureId);
        public static bool operator ==(NavigationSurfaceId left, NavigationSurfaceId right) => left.Equals(right);
        public static bool operator !=(NavigationSurfaceId left, NavigationSurfaceId right) => !left.Equals(right);
    }

    /// <summary>Classifies captured geometry for collision and support queries.</summary>
    public enum NavigationSurfaceKind
    {
        Solid,
        OneWay,
    }

    /// <summary>Shape primitive used by a detached navigation snapshot.</summary>
    public enum NavigationShapeType
    {
        Polygon,
        Edge,
        Circle,
        Capsule,
    }

    /// <summary>
    /// Immutable body support result. Position is the body's lower-center anchor, whose x coordinate
    /// remains the queried body center even when only part of the feet overlap the supporting surface.
    /// </summary>
    public readonly struct NavigationSupport
    {
        public NavigationSurfaceId Surface { get; }
        public NavigationSurfaceKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2 Normal { get; }

        public NavigationSupport(NavigationSurfaceId surface, NavigationSurfaceKind kind, Vector2 position, Vector2 normal)
        {
            if (!IsFinite(position) || !IsFinite(normal)) throw new ArgumentException("Navigation support must be finite.");
            Surface = surface;
            Kind = kind;
            Position = position;
            Normal = normal;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>One immutable geometry candidate owned by a navigation snapshot.</summary>
    public readonly struct NavigationSupportCandidate
    {
        /// <summary>Gets the candidate identity, unique only within its owning snapshot.</summary>
        public int Id { get; }

        /// <summary>Gets the physical support represented by this search candidate.</summary>
        public NavigationSupport Support { get; }

        /// <summary>Creates a snapshot-owned support candidate.</summary>
        public NavigationSupportCandidate(int id, NavigationSupport support)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
            Support = support;
        }
    }

    /// <summary>One one-way surface crossing along a trajectory segment.</summary>
    public readonly struct NavigationSurfaceCrossing
    {
        public NavigationSurfaceId Surface { get; }
        public Vector2 Position { get; }
        public Vector2 Normal { get; }
        public float Fraction { get; }

        public NavigationSurfaceCrossing(NavigationSurfaceId surface, Vector2 position, Vector2 normal, float fraction)
        {
            if (!IsFinite(position) || !IsFinite(normal) || !IsFinite(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentException("Navigation crossing must be finite and normalized.");
            Surface = surface;
            Position = position;
            Normal = normal;
            Fraction = fraction;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Detached geometry captured from one source collider or geometry provider.</summary>
    public readonly struct NavigationShapeData
    {
        public int SourceId { get; }
        public int FeatureId { get; }
        public NavigationShapeType ShapeType { get; }
        public IReadOnlyList<Vector2> Vertices { get; }
        public float Radius { get; }
        public NavigationSurfaceKind Kind { get; }
        public bool HasSupport { get; }
        public Vector2 OneWayDirection { get; }
        public float OneWayCosHalfArc { get; }
        public float DirectedNormalSign { get; }

        public NavigationShapeData(int sourceId, int featureId, NavigationShapeType shapeType,
            IReadOnlyList<Vector2> vertices, float radius, NavigationSurfaceKind kind, bool hasSupport,
            Vector2 oneWayDirection = default, float oneWayCosHalfArc = 1f, float directedNormalSign = 0f)
        {
            if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (featureId < 0) throw new ArgumentOutOfRangeException(nameof(featureId));
            if (!Enum.IsDefined(typeof(NavigationShapeType), shapeType)) throw new ArgumentOutOfRangeException(nameof(shapeType));
            if (vertices == null || vertices.Count == 0) throw new ArgumentException("A navigation shape needs vertices.", nameof(vertices));
            if (!IsFinite(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!IsFinite(oneWayDirection) || !IsFinite(oneWayCosHalfArc) || oneWayCosHalfArc < -1f || oneWayCosHalfArc > 1f)
                throw new ArgumentException("One-way direction data must be finite and normalized.");
            if (!IsFinite(directedNormalSign)) throw new ArgumentOutOfRangeException(nameof(directedNormalSign));
            for (int index = 0; index < vertices.Count; index++)
                if (!IsFinite(vertices[index])) throw new ArgumentException("Shape vertices must be finite.", nameof(vertices));

            SourceId = sourceId;
            FeatureId = featureId;
            ShapeType = shapeType;
            Vertices = Copy(vertices);
            Radius = radius;
            Kind = kind;
            HasSupport = hasSupport;
            OneWayDirection = oneWayDirection;
            OneWayCosHalfArc = oneWayCosHalfArc;
            DirectedNormalSign = directedNormalSign;
        }

        private static Vector2[] Copy(IReadOnlyList<Vector2> source)
        {
            Vector2[] copy = new Vector2[source.Count];
            for (int index = 0; index < copy.Length; index++) copy[index] = source[index];
            return copy;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Maps a non-overlapping cell region to a captured project region identity.</summary>
    public readonly struct NavigationRegionData
    {
        public RectInt CellBounds { get; }
        public int RegionId { get; }

        public NavigationRegionData(RectInt cellBounds, int regionId)
        {
            if (cellBounds.width <= 0 || cellBounds.height <= 0) throw new ArgumentException("Region bounds must be positive.", nameof(cellBounds));
            CellBounds = cellBounds;
            RegionId = regionId;
        }
    }

    /// <summary>
    /// Provides immutable world-space navigation queries. Implementations must not retain Unity objects,
    /// access Physics2D, or mutate storage while planner threads are reading the snapshot.
    /// </summary>
    public interface INavigationWorld
    {
        Vector2 Origin { get; }
        float CellSize { get; }
        RectInt CellBounds { get; }
        bool IsBodyClear(Rect body, float surfaceContactTolerance);
        bool IsBodyPathClear(Rect startBody, Vector2 displacement, float surfaceContactTolerance);
        bool IsLineOfSightClear(Vector2 start, Vector2 end);
        /// <summary>
        /// Resolves physical support for a body lower-center anchor. A valid support may contact any
        /// overlapping part of the body's foot interval; it is not limited to a center-ray hit.
        /// </summary>
        bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support);
        void CollectSupportCandidates(Rect anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results);
        void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth, List<NavigationSurfaceCrossing> results);
        bool AreInSameRegion(Vector2 first, Vector2 second);
    }
}
