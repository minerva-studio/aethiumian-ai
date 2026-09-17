using System;
using System.Collections.Generic;
using UnityEngine;
using static Aethiumian.AI.Navigation.NavigationArithmetic;

namespace Aethiumian.AI.Navigation
{
    public sealed class Shape
    {
        public readonly int SourceId;
        public readonly int FeatureId;
        public readonly NavigationShapeType ShapeType;
        public readonly Vector2[] Vertices;
        public readonly float Radius;
        public readonly NavigationSurfaceKind Kind;
        public readonly bool HasSupport;
        public readonly Vector2 OneWayDirection;
        public readonly float OneWayCosHalfArc;
        public readonly float DirectedNormalSign;
        public readonly Vector2 Min;
        public readonly Vector2 Max;


        public Shape(NavigationShapeData data)
        {
            SourceId = data.SourceId;
            FeatureId = data.FeatureId;
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
            Min = min - Vector2.one * Radius;
            Max = max + Vector2.one * Radius;
        }

        public NavigationSurfaceId SurfaceId => new NavigationSurfaceId(SourceId, FeatureId);






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


        public bool IsShapeIntersection(Rect rect)
        {
            switch (ShapeType)
            {
                case NavigationShapeType.Circle: return CircleIntersectsRect(Vertices[0], Radius, rect);
                case NavigationShapeType.Capsule: return SegmentIntersectsExpandedRect(Vertices[0], Vertices[1], Radius, rect);
                case NavigationShapeType.Edge:
                    for (int index = 1; index < Vertices.Length; index++)
                        if (SegmentIntersectsRectInterior(Vertices[index - 1], Vertices[index], rect)) return true;
                    return false;
                default: return PolygonIntersectsRect(Vertices, rect);
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
