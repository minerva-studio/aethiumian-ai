using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Identifies a captured navigation surface within one immutable snapshot.</summary>
    public readonly struct NavigationSurfaceId : IEquatable<NavigationSurfaceId>, IComparable<NavigationSurfaceId>
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
        public int CompareTo(NavigationSurfaceId other)
        {
            int sourceComparison = SourceId.CompareTo(other.SourceId);
            return sourceComparison != 0 ? sourceComparison : FeatureId.CompareTo(other.FeatureId);
        }


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
            if (!NavigationNumeric.IsFinite(position) || !NavigationNumeric.IsFinite(normal) || !NavigationNumeric.IsFinite(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentException("Navigation crossing must be finite and normalized.");
            Surface = surface;
            Position = position;
            Normal = normal;
            Fraction = fraction;
        }

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
        /// <summary>
        /// Zero denotes an undirected edge. A nonzero sign preserves the captured edge's
        /// right-hand normal, including orientation changes from mirrored transforms.
        /// </summary>
        public float DirectedNormalSign { get; }

        public NavigationShapeData(int sourceId, int featureId, NavigationShapeType shapeType,
            IReadOnlyList<Vector2> vertices, float radius, NavigationSurfaceKind kind, bool hasSupport,
            Vector2 oneWayDirection = default, float oneWayCosHalfArc = 1f, float directedNormalSign = 0f)
        {
            if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (featureId < 0) throw new ArgumentOutOfRangeException(nameof(featureId));
            if (!Enum.IsDefined(typeof(NavigationShapeType), shapeType)) throw new ArgumentOutOfRangeException(nameof(shapeType));
            if (vertices == null || vertices.Count == 0) throw new ArgumentException("A navigation shape needs vertices.", nameof(vertices));
            if (!NavigationNumeric.IsFinite(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!NavigationNumeric.IsFinite(oneWayDirection) || !NavigationNumeric.IsFinite(oneWayCosHalfArc) || oneWayCosHalfArc < -1f || oneWayCosHalfArc > 1f)
                throw new ArgumentException("One-way direction data must be finite and normalized.");
            if (!NavigationNumeric.IsFinite(directedNormalSign)) throw new ArgumentOutOfRangeException(nameof(directedNormalSign));
            for (int index = 0; index < vertices.Count; index++)
                if (!NavigationNumeric.IsFinite(vertices[index])) throw new ArgumentException("Shape vertices must be finite.", nameof(vertices));

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

    }

    /// <summary>Maps a non-overlapping world-space region rectangle to a captured project region identity.</summary>
    public readonly struct NavigationRegionData
    {
        /// <summary>Gets the world-space rectangle covered by this region. The rectangle is half-open.</summary>
        public Rect WorldBounds { get; }
        public int RegionId { get; }

        public NavigationRegionData(Rect worldBounds, int regionId)
        {
            if (!NavigationNumeric.IsFinite(worldBounds.min) || !NavigationNumeric.IsFinite(worldBounds.max)
                || worldBounds.width <= 0f || worldBounds.height <= 0f)
                throw new ArgumentException("Region bounds must be positive and finite.", nameof(worldBounds));
            WorldBounds = worldBounds;
            RegionId = regionId;
        }
    }

    /// <summary>
    /// Provides immutable world-space navigation queries. Implementations must not retain Unity objects,
    /// access Physics2D, or mutate captured geometry while planner threads are reading it. Implementations
    /// may maintain private, thread-safe, bounded derived-computation caches.
    /// </summary>
    public interface INavigationWorld
    {
        /// <summary>
        /// World-space rectangle of the captured finite world. Geometry outside it is never navigable.
        /// </summary>
        Rect WorldBounds { get; }

        /// <summary>
        /// Checks body clearance against captured geometry with the supplied contact tolerance.
        /// </summary>
        bool IsBodyClear(Rect body, float surfaceContactTolerance);

        /// <summary>
        /// Checks clearance along a body's displacement through captured geometry.
        /// </summary>
        bool IsBodyPathClear(Rect startBody, Vector2 displacement, float surfaceContactTolerance);

        /// <summary>
        /// Checks whether captured geometry permits line of sight between two positions.
        /// </summary>
        bool IsLineOfSightClear(Vector2 start, Vector2 end);

        /// <summary>
        /// Resolves physical support for a body lower-center anchor. A valid support may contact any
        /// overlapping part of the body's foot interval; it is not limited to a center-ray hit.
        /// </summary>
        bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support);

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
        IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(Rect anchorBounds, Vector2 bodySize);

        /// <summary>
        /// Appends support candidates to the supplied list without clearing existing entries.
        /// </summary>
        void CollectSupportCandidates(Rect anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results);

        /// <summary>
        /// Collects directed one-way surface crossings along a lower-center body segment.
        /// </summary>
        void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth, List<NavigationSurfaceCrossing> results);

        /// <summary>
        /// Checks whether two positions belong to the same captured navigation region.
        /// </summary>
        bool AreInSameRegion(Vector2 first, Vector2 second);
    }
}
