using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Exact immutable identity for goal cache and target-motion reuse decisions.</summary>
    public readonly struct NavigationGoalKey : IEquatable<NavigationGoalKey>
    {
        public Bounds TargetBounds { get; }
        public DistanceMetric DistanceMetric { get; }
        public bool RequiresLineOfSight { get; }
        public float ArrivalTolerance { get; }
        public float RetreatDistance { get; }
        public float SnapshotCellSize { get; }
        public NavigationGoalGeometry Geometry { get; }

        internal NavigationGoalKey(Bounds targetBounds, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance, float retreatDistance, float snapshotCellSize)
        {
            Validate.Bounds(targetBounds, nameof(targetBounds));
            if (!Enum.IsDefined(typeof(NavigationGoalGeometry), geometry)) throw new ArgumentOutOfRangeException(nameof(geometry));
            if (!Enum.IsDefined(typeof(DistanceMetric), distanceMetric)) throw new ArgumentOutOfRangeException(nameof(distanceMetric));
            Validate.NonNegativeFinite(arrivalTolerance, nameof(arrivalTolerance));
            Validate.NonNegativeFinite(retreatDistance, nameof(retreatDistance));
            if (!NavigationNumeric.IsFinite(snapshotCellSize) || snapshotCellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(snapshotCellSize), snapshotCellSize, "Snapshot cell size must be finite and positive.");
            TargetBounds = targetBounds;
            Geometry = geometry;
            DistanceMetric = distanceMetric;
            RequiresLineOfSight = requiresLineOfSight;
            ArrivalTolerance = arrivalTolerance;
            RetreatDistance = retreatDistance;
            SnapshotCellSize = snapshotCellSize;
        }

        internal NavigationGoalKey(Bounds targetBounds, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance, float snapshotCellSize)
            : this(targetBounds, geometry, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f, snapshotCellSize)
        {
        }

        public bool Equals(NavigationGoalKey other)
        {
            return TargetBounds.Equals(other.TargetBounds)
                        && Geometry == other.Geometry
                        && DistanceMetric == other.DistanceMetric
                        && RequiresLineOfSight == other.RequiresLineOfSight
                        && ArrivalTolerance.Equals(other.ArrivalTolerance)
                        && RetreatDistance.Equals(other.RetreatDistance)
                        && SnapshotCellSize.Equals(other.SnapshotCellSize);
        }

        public override bool Equals(object obj) => obj is NavigationGoalKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TargetBounds, Geometry, DistanceMetric, RequiresLineOfSight, ArrivalTolerance, RetreatDistance, SnapshotCellSize);

    }

}
