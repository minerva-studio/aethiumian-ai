using System;

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
}
