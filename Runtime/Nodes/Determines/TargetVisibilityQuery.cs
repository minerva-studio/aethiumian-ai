using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Evaluates target visibility using IsInVision's blocking-layer and selected DistanceTo semantics.</summary>
    public static class TargetVisibilityQuery
    {
        /// <summary>Returns whether a target can be seen from an origin under the supplied distance and layer rules.</summary>
        public static bool IsVisible(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D sourceCollider,
            Collider2D targetCollider,
            LayerMask blockingLayers,
            float maxDistance = -1f,
            DistanceTo.Measurement measurement = DistanceTo.Measurement.ColliderSurface)
        {
            Vector2 displacement = targetPosition - origin;
            float realDistance = GetDistance(
                origin,
                targetPosition,
                sourceCollider,
                targetCollider,
                measurement);
            float raycastMagnitude = maxDistance > 0f ? maxDistance : displacement.magnitude;
            RaycastHit2D hit = Physics2D.Raycast(origin, displacement, raycastMagnitude, blockingLayers);
            if (hit.collider)
            {
                if (realDistance <= hit.distance) return true;
                return false;
            }

            if (maxDistance <= 0f) return true;
            return realDistance <= maxDistance;
        }

        /// <summary>Computes the selected source-to-target distance used by the visibility contract.</summary>
        private static float GetDistance(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D sourceCollider,
            Collider2D targetCollider,
            DistanceTo.Measurement measurement)
        {
            if (measurement == DistanceTo.Measurement.TransformPosition)
                return (targetPosition - origin).magnitude;

            if (sourceCollider && targetCollider)
            {
                GameObject sourceObject = sourceCollider.attachedRigidbody
                    ? sourceCollider.attachedRigidbody.gameObject
                    : sourceCollider.gameObject;
                GameObject targetObject = targetCollider.attachedRigidbody
                    ? targetCollider.attachedRigidbody.gameObject
                    : targetCollider.gameObject;
                return DistanceTo.Measure(
                    sourceObject,
                    targetObject,
                    measurement,
                    DistanceTo.DistanceType.Euclidean);
            }

            if (!sourceCollider && !targetCollider)
                return (targetPosition - origin).magnitude;
            if (sourceCollider)
                return (sourceCollider.ClosestPoint(targetPosition) - targetPosition).magnitude;
            return (targetCollider.ClosestPoint(origin) - origin).magnitude;
        }
    }
}
