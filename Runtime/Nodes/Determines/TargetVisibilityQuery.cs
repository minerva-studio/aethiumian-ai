using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Evaluates target visibility using the same collider and blocking-layer semantics as IsInVision.</summary>
    public static class TargetVisibilityQuery
    {
        /// <summary>Returns whether a target can be seen from an origin under the supplied distance and layer rules.</summary>
        public static bool IsVisible(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D sourceCollider,
            Collider2D targetCollider,
            LayerMask blockingLayers,
            float maxDistance = -1f)
        {
            Vector2 displacement = targetPosition - origin;
            float realDistance = GetColliderDistance(origin, targetPosition, sourceCollider, targetCollider);
            float raycastMagnitude = maxDistance > 0f ? maxDistance : displacement.magnitude;
            RaycastHit2D hit = Physics2D.Raycast(origin, displacement, raycastMagnitude, blockingLayers);
            if (hit.collider)
            {
                if (realDistance < hit.distance) return true;
                return false;
            }

            if (maxDistance <= 0f) return true;
            return realDistance < maxDistance;
        }

        /// <summary>Computes the closest supported source-to-target distance used by the visibility contract.</summary>
        private static float GetColliderDistance(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D sourceCollider,
            Collider2D targetCollider)
        {
            if (sourceCollider && targetCollider)
            {
                ColliderDistance2D distance = targetCollider.Distance(sourceCollider);
                if (distance.isValid) return distance.distance;
            }

            if (!sourceCollider && !targetCollider)
                return (targetPosition - origin).magnitude;
            if (sourceCollider)
                return (sourceCollider.ClosestPoint(targetPosition) - targetPosition).magnitude;
            return (targetCollider.ClosestPoint(origin) - origin).magnitude;
        }
    }
}
