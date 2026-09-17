using System;
using System.Collections.Generic;
using UnityEngine;

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

        private static Vector2[] Copy(IReadOnlyList<Vector2> source)
        {
            Vector2[] result = new Vector2[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
