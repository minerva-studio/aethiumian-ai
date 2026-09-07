using Aethiumian.AI.Variables;
using Aethiumian.AI.Geometry;
using Aethiumian.AI.Attributes;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    [NodeTip("Reads the distance between the entity and a target.")]
    public sealed class DistanceTo : ComparableDetermine<float>
    {
        public enum Measurement
        {
            /// <summary>Choose ColliderBounds when both objects have usable colliders; otherwise choose TransformPosition.</summary>
            [InspectorName("Default (auto)")]
            Default = 0,

            /// <summary>Measure between the source and target Transform positions.</summary>
            [InspectorName("Transform position")]
            TransformPosition = 1,

            /// <summary>Measure the selected metric between merged source and target Collider2D AABBs.</summary>
            [InspectorName("Collider AABB gap")]
            ColliderBounds = 2,

            /// <summary>Measure the closest physical distance between source and target Collider2D shapes.</summary>
            [InspectorName("Collider surface gap")]
            ColliderSurface = 3,
        }

        public enum DistanceType
        {
            /// <summary>
            /// the magitude of displacement
            /// </summary>
            Euclidean,

            /// <summary>
            /// the sum of displacements of all coordination
            /// </summary>
            Manhattan,

            /// <summary>
            /// the maximum of displacement of all coordination
            /// </summary>
            Chebyshev,
        }

        public Measurement measurement = Measurement.Default;
        [DisplayIf(nameof(measurement), false, Measurement.ColliderSurface)]
        public DistanceType distanceType;
        [Readable]
        public VariableReference<GameObject> @object;

        public override float GetValue()
        {
            if (!@object.HasValue) return float.PositiveInfinity;
            GameObject target = @object.GameObjectValue;
            if (!target) return float.PositiveInfinity;

            return Measure(gameObject, target, measurement, distanceType);
        }

        /// <summary>Measures one source and target using the configured geometry and metric contracts.</summary>
        internal static float Measure(
            GameObject source,
            GameObject target,
            Measurement measurement,
            DistanceType distanceType)
        {
            if (!source || !target) return float.PositiveInfinity;

            Measurement resolvedMeasurement = ResolveMeasurement(source, target, measurement);
            return resolvedMeasurement switch
            {
                Measurement.TransformPosition => Distance(source.transform.position, target.transform.position, distanceType),
                Measurement.ColliderBounds => DistanceBetweenBounds(
                    ColliderGeometryQuery.GetMergedBounds(GetRequiredColliders(source, "source")),
                    ColliderGeometryQuery.GetMergedBounds(GetRequiredColliders(target, "target")),
                    distanceType),
                Measurement.ColliderSurface => ColliderGeometryQuery.GetSurfaceDistance(
                    GetRequiredColliders(source, "source"),
                    GetRequiredColliders(target, "target")),
                _ => throw new ArgumentOutOfRangeException(nameof(measurement), measurement,
                    "Unknown DistanceTo measurement."),
            };
        }

        /// <summary>
        /// Resolves the automatic measurement from the current usable body geometry.
        /// Both objects must expose an enabled, non-trigger Collider2D for body-gap measurement;
        /// point measurement remains the safe fallback for position-only targets.
        /// </summary>
        internal static Measurement ResolveMeasurement(
            GameObject source,
            GameObject target,
            Measurement measurement)
        {
            if (measurement != Measurement.Default) return measurement;

            bool sourceHasCollider = ColliderGeometryQuery.GetColliders(source).Length > 0;
            bool targetHasCollider = ColliderGeometryQuery.GetColliders(target).Length > 0;
            return sourceHasCollider && targetHasCollider
                ? Measurement.ColliderBounds
                : Measurement.TransformPosition;
        }

        public float Distance(Vector2 position, DistanceType distanceType)
        {
            return Distance(transform.position, position, distanceType);
        }

        /// <summary>Measures the selected metric between two point positions.</summary>
        private static float Distance(Vector2 source, Vector2 target, DistanceType distanceType)
        {
            Vector2 displacement = source - target;
            float absoluteX = Mathf.Abs(displacement.x);
            float absoluteY = Mathf.Abs(displacement.y);
            switch (distanceType)
            {
                case DistanceType.Manhattan:
                    return absoluteX + absoluteY;
                case DistanceType.Chebyshev:
                    return Mathf.Max(absoluteX, absoluteY);
                case DistanceType.Euclidean:
                default:
                    return displacement.magnitude;
            }
        }

        /// <summary>Measures the selected metric between two world-space AABBs.</summary>
        private static float DistanceBetweenBounds(Bounds source, Bounds target, DistanceType metric)
        {
            float x = Mathf.Max(target.min.x - source.max.x, source.min.x - target.max.x, 0f);
            float y = Mathf.Max(target.min.y - source.max.y, source.min.y - target.max.y, 0f);
            return metric switch
            {
                DistanceType.Euclidean => Mathf.Sqrt(x * x + y * y),
                DistanceType.Manhattan => x + y,
                DistanceType.Chebyshev => Mathf.Max(x, y),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric,
                    "Unknown DistanceTo metric."),
            };
        }

        /// <summary>Gets the required active Collider2D set for one configured distance measurement.</summary>
        private static Collider2D[] GetRequiredColliders(GameObject owner, string role)
        {
            Collider2D[] colliders = ColliderGeometryQuery.GetColliders(owner);
            if (colliders.Length == 0)
            {
                throw new MissingComponentException(
                    $"DistanceTo requires an enabled non-trigger Collider2D on the {role} object '{owner.name}'.");
            }

            return colliders;
        }
    }
}
