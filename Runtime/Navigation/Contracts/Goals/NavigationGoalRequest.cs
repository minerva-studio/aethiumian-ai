using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Immutable, Unity-scene-independent data captured at a movement caller boundary.</summary>
    public readonly struct NavigationGoalRequest
    {
        public Bounds TargetBounds { get; }
        public DistanceMetric DistanceMetric { get; }
        public bool RequiresLineOfSight { get; }
        public float ArrivalTolerance { get; }
        public float RetreatDistance { get; }
        public NavigationGoalGeometry Geometry { get; }

        public NavigationGoalRequest(Bounds targetBounds, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance, float retreatDistance)
        {
            ValidateMetric(distanceMetric);
            Validate.Bounds(targetBounds, nameof(targetBounds));
            Validate.NonNegativeFinite(arrivalTolerance, nameof(arrivalTolerance));
            Validate.NonNegativeFinite(retreatDistance, nameof(retreatDistance));
            TargetBounds = targetBounds;
            Geometry = geometry;
            DistanceMetric = distanceMetric;
            RequiresLineOfSight = requiresLineOfSight;
            ArrivalTolerance = arrivalTolerance;
            RetreatDistance = retreatDistance;
        }

        public static NavigationGoalRequest GroundRange(Bounds targetBounds, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.GroundRange, DistanceMetric.Euclidean, requiresLineOfSight, arrivalTolerance, 0f);

        public static NavigationGoalRequest Proximity(Bounds targetBounds, DistanceMetric distanceMetric, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.Proximity, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an open-ended goal that completes after the mover is far enough from the target.</summary>
        public static NavigationGoalRequest Retreat(Bounds targetBounds, DistanceMetric distanceMetric, float retreatDistance)
            => new(targetBounds, NavigationGoalGeometry.Retreat, distanceMetric, false, 0f, retreatDistance);

        public static NavigationGoalRequest Point(Vector2 destination, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance)
            => new(new Bounds(destination, Vector3.zero), geometry, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an equivalent request with only its captured target bounds replaced.</summary>
        public NavigationGoalRequest WithTargetBounds(Bounds targetBounds)
            => new(targetBounds, Geometry, DistanceMetric, RequiresLineOfSight, ArrivalTolerance, RetreatDistance);


        private static void ValidateMetric(DistanceMetric value)
        {
            if (!Enum.IsDefined(typeof(DistanceMetric), value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown distance metric.");
        }
    }
}
