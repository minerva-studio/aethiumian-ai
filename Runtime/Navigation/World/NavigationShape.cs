using System;
using System.Collections.Generic;
using UnityEngine;
using static Aethiumian.AI.Navigation.NavigationArithmetic;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Shape primitive used by a detached navigation snapshot.</summary>
    public enum NavigationShapeType
    {
        Polygon,
        Edge,
        Circle,
        Capsule,
    }

    /// <summary>
    /// Classifies captured geometry for collision and support queries.
    /// </summary>
    public enum NavigationSurfaceKind
    {
        Solid,
        OneWay,
    }


    /// <summary>
    /// Detached geometry captured from one source collider or geometry provider.
    /// </summary>
    public readonly struct NavigationShapeData
    {
        public NavigationSurfaceId SurfaceId { get; }
        public NavigationShapeType ShapeType { get; }
        public NavigationSurfaceKind Kind { get; }
        public IReadOnlyList<Vector2> Vertices { get; }
        public float Radius { get; }
        public bool HasSupport { get; }
        public Vector2 OneWayDirection { get; }
        public float OneWayCosHalfArc { get; }
        /// <summary>
        /// Zero denotes an undirected edge. A nonzero sign preserves the captured edge's
        /// right-hand normal, including orientation changes from mirrored transforms.
        /// </summary>
        public float DirectedNormalSign { get; }

        public NavigationShapeData(
            int sourceId,
            int featureId,
            NavigationShapeType shapeType,
            IReadOnlyList<Vector2> vertices,
            float radius,
            NavigationSurfaceKind kind,
            bool hasSupport,
            Vector2 oneWayDirection = default,
            float oneWayCosHalfArc = 1f,
            float directedNormalSign = 0f)
            : this(new NavigationSurfaceId(sourceId, featureId), shapeType, vertices, radius, kind, hasSupport, oneWayDirection, oneWayCosHalfArc, directedNormalSign)
        {
        }

        public NavigationShapeData(
            NavigationSurfaceId surfaceId,
            NavigationShapeType shapeType,
            IReadOnlyList<Vector2> vertices,
            float radius,
            NavigationSurfaceKind kind,
            bool hasSupport,
            Vector2 oneWayDirection = default,
            float oneWayCosHalfArc = 1f,
            float directedNormalSign = 0f)
        {
            if (!Enum.IsDefined(typeof(NavigationShapeType), shapeType)) throw new ArgumentOutOfRangeException(nameof(shapeType));
            if (vertices == null || vertices.Count == 0) throw new ArgumentException("A navigation shape needs vertices.", nameof(vertices));
            if (!NavigationNumeric.IsFinite(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!NavigationNumeric.IsFinite(oneWayDirection) || !NavigationNumeric.IsFinite(oneWayCosHalfArc) || oneWayCosHalfArc < -1f || oneWayCosHalfArc > 1f)
                throw new ArgumentException("One-way direction data must be finite and normalized.");
            if (!NavigationNumeric.IsFinite(directedNormalSign)) throw new ArgumentOutOfRangeException(nameof(directedNormalSign));
            for (int index = 0; index < vertices.Count; index++)
                if (!NavigationNumeric.IsFinite(vertices[index])) throw new ArgumentException("Shape vertices must be finite.", nameof(vertices));

            SurfaceId = surfaceId;
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

    /// <summary>
    /// Persistent geometry captured from one source collider or geometry provider, including its surface identity and bounding box.
    /// </summary>
    public sealed class Shape
    {
        public readonly NavigationSurfaceId SurfaceId;
        public readonly NavigationShapeType ShapeType;
        public readonly NavigationSurfaceKind Kind;
        public readonly Vector2[] Vertices;
        public readonly float Radius;
        public readonly bool HasSupport;
        public readonly Vector2 OneWayDirection;
        public readonly float OneWayCosHalfArc;
        public readonly float DirectedNormalSign;

        /// <summary>
        /// Continuous bounding box of the shape, including its radius.
        /// (Cached result from the vertices and radius)
        /// </summary>
        public readonly AABB Bounds;


        public Shape(NavigationShapeData data)
        {
            SurfaceId = data.SurfaceId;
            ShapeType = data.ShapeType;
            Vertices = Copy(data.Vertices);
            Radius = data.Radius;
            Kind = data.Kind;
            HasSupport = data.HasSupport;
            OneWayDirection = data.OneWayDirection;
            OneWayCosHalfArc = data.OneWayCosHalfArc;
            DirectedNormalSign = data.DirectedNormalSign;
            Vector2 min = Vertices[0];
            Vector2 max = Vertices[0];
            for (int index = 1; index < Vertices.Length; index++) { min = Vector2.Min(min, Vertices[index]); max = Vector2.Max(max, Vertices[index]); }
            Bounds = new AABB(min.x - Radius, min.y - Radius, max.x + Radius, max.y + Radius);
        }







        public bool IsAllowedSupport(Vector2 normal)
        {
            return normal.y > NavigationConstant.Epsilon && (Kind != NavigationSurfaceKind.OneWay || Vector2.Dot(normal, OneWayDirection) >= OneWayCosHalfArc - NavigationConstant.Epsilon);
        }

        public bool TryGetSurfaceAtX(float x, out float y, out Vector2 normal, float maxSurfaceY = float.PositiveInfinity, bool supportedOnly = false)
        {
            const float Epsilon = NavigationConstant.Epsilon;
            y = float.NegativeInfinity;
            normal = Vector2.up;
            bool found = false;
            switch (ShapeType)
            {
                case NavigationShapeType.Circle:
                    found = TryGetCircleSurface(Vertices[0], Radius, x, out y, out normal);
                    if (found && (y > maxSurfaceY + Epsilon || supportedOnly && !IsAllowedSupport(normal))) found = false;
                    break;
                case NavigationShapeType.Capsule:
                    found = TryGetCapsuleSurface(x, out y, out normal);
                    if (found && (y > maxSurfaceY + Epsilon || supportedOnly && !IsAllowedSupport(normal))) found = false;
                    break;
                case NavigationShapeType.Edge:
                    for (int index = 1; index < Vertices.Length; index++)
                        if (TryGetSegmentSurface(Vertices[index - 1], Vertices[index], x, out float edgeY, out Vector2 edgeNormal, polygonSign: DirectedNormalSign, orientUp: DirectedNormalSign == 0f)
                            && edgeY <= maxSurfaceY + Epsilon
                            && (!supportedOnly || IsAllowedSupport(edgeNormal))
                            && (!found || edgeY > y)) { y = edgeY; normal = edgeNormal; found = true; }
                    break;
                default:
                    float winding = PolygonWinding(Vertices);
                    for (int index = 0; index < Vertices.Length; index++)
                    {
                        Vector2 a = Vertices[index];
                        Vector2 b = Vertices[(index + 1) % Vertices.Length];
                        if (TryGetSegmentSurface(a, b, x, out float polygonY, out Vector2 polygonNormal, winding >= 0f ? 1f : -1f, orientUp: !supportedOnly)
                            && polygonY <= maxSurfaceY + Epsilon
                            && (!supportedOnly || IsAllowedSupport(polygonNormal))
                            && (!found || polygonY > y)) { y = polygonY; normal = polygonNormal; found = true; }
                    }
                    break;
            }
            return found;
        }

        public bool TryGetCapsuleSurface(float x, out float y, out Vector2 normal)
        {
            y = float.NegativeInfinity; normal = Vector2.up; bool found = false;
            Vector2 a = Vertices[0];
            Vector2 b = Vertices[Mathf.Min(1, Vertices.Length - 1)];
            Vector2 direction = b - a;
            float length = direction.magnitude;
            if (length <= NavigationConstant.Epsilon) return TryGetCircleSurface(a, Radius, x, out y, out normal);
            Vector2 side = new Vector2(-direction.y, direction.x) / length;
            if (TryGetSegmentSurface(a + side * Radius, b + side * Radius, x, out float candidateY, out Vector2 candidateNormal)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetSegmentSurface(a - side * Radius, b - side * Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetCircleSurface(a, Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetCircleSurface(b, Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            return found;
        }


        public bool IsShapeIntersection(AABB bounds)
        {
            switch (ShapeType)
            {
                case NavigationShapeType.Circle: return CircleIntersectsAabb(Vertices[0], Radius, bounds);
                case NavigationShapeType.Capsule: return SegmentIntersectsExpandedAabb(Vertices[0], Vertices[1], Radius, bounds);
                case NavigationShapeType.Edge:
                    for (int index = 1; index < Vertices.Length; index++)
                        if (SegmentIntersectsAabbInterior(Vertices[index - 1], Vertices[index], bounds)) return true;
                    return false;
                default: return PolygonIntersectsAabb(Vertices, bounds);
            }
        }

        public bool IsShapeSegmentIntersection(Vector2 start, Vector2 end)
        {
            switch (ShapeType)
            {
                case NavigationShapeType.Circle: return DistanceToSegment(Vertices[0], start, end) <= Radius + NavigationConstant.Epsilon;
                case NavigationShapeType.Capsule: return DistanceBetweenSegments(Vertices[0], Vertices[1], start, end) <= Radius + NavigationConstant.Epsilon;
                case NavigationShapeType.Edge:
                    for (int index = 1; index < Vertices.Length; index++)
                        if (SegmentsIntersect(Vertices[index - 1], Vertices[index], start, end)) return true;
                    return false;
                default:
                    if (PointInPolygon(start, Vertices) || PointInPolygon(end, Vertices)) return true;
                    for (int index = 0; index < Vertices.Length; index++)
                        if (SegmentsIntersect(Vertices[index], Vertices[(index + 1) % Vertices.Length], start, end)) return true;
                    return false;
            }
        }





        private static Vector2[] Copy(IReadOnlyList<Vector2> source)
        {
            Vector2[] result = new Vector2[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
