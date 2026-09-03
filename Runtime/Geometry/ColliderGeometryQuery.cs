using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Geometry
{
    /// <summary>Provides the shared Collider2D filtering and bounds contract used by AI distance and movement.</summary>
    internal static class ColliderGeometryQuery
    {
        /// <summary>Gets enabled, non-trigger colliders associated with the supplied Rigidbody2D hierarchy.</summary>
        internal static Collider2D[] GetColliders(Rigidbody2D body)
        {
            if (!body) throw new ArgumentNullException(nameof(body));

            Collider2D[] candidates = body.GetComponentsInChildren<Collider2D>(true);
            List<Collider2D> result = new(candidates.Length);
            for (int index = 0; index < candidates.Length; index++)
            {
                Collider2D collider = candidates[index];
                if (collider && collider.isActiveAndEnabled && !collider.isTrigger && collider.attachedRigidbody == body)
                {
                    result.Add(collider);
                }
            }

            return result.ToArray();
        }

        /// <summary>Gets enabled, non-trigger colliders from a target hierarchy using its first Rigidbody2D association.</summary>
        internal static Collider2D[] GetTargetColliders(GameObject target)
        {
            if (!target) return Array.Empty<Collider2D>();

            Rigidbody2D targetBody = target.GetComponentInChildren<Rigidbody2D>();
            Collider2D[] candidates = target.GetComponentsInChildren<Collider2D>(true);
            List<Collider2D> result = new(candidates.Length);
            for (int index = 0; index < candidates.Length; index++)
            {
                Collider2D collider = candidates[index];
                if (!collider || !collider.isActiveAndEnabled || collider.isTrigger) continue;
                if (targetBody && collider.attachedRigidbody != targetBody) continue;
                result.Add(collider);
            }

            return result.ToArray();
        }

        /// <summary>Gets enabled, non-trigger colliders from a hierarchy, preferring its Rigidbody2D association.</summary>
        internal static Collider2D[] GetColliders(GameObject root)
        {
            if (!root) return Array.Empty<Collider2D>();
            Rigidbody2D body = root.GetComponentInChildren<Rigidbody2D>();
            return body ? GetColliders(body) : GetTargetColliders(root);
        }

        /// <summary>Gets the merged world-space AABB of a non-empty collider collection.</summary>
        internal static Bounds GetMergedBounds(IReadOnlyList<Collider2D> colliders)
        {
            if (colliders == null) throw new ArgumentNullException(nameof(colliders));

            Bounds bounds = default;
            bool hasBounds = false;
            for (int index = 0; index < colliders.Count; index++)
            {
                Collider2D collider = colliders[index];
                if (!collider || !collider.isActiveAndEnabled || collider.isTrigger) continue;
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!hasBounds)
                throw new ArgumentException("At least one enabled non-trigger collider is required.", nameof(colliders));
            return bounds;
        }

        /// <summary>Gets the closest non-negative physical distance between two collider collections.</summary>
        internal static float GetSurfaceDistance(
            IReadOnlyList<Collider2D> sourceColliders,
            IReadOnlyList<Collider2D> targetColliders)
        {
            if (sourceColliders == null) throw new ArgumentNullException(nameof(sourceColliders));
            if (targetColliders == null) throw new ArgumentNullException(nameof(targetColliders));

            float closest = float.PositiveInfinity;
            for (int sourceIndex = 0; sourceIndex < sourceColliders.Count; sourceIndex++)
            {
                Collider2D source = sourceColliders[sourceIndex];
                if (!source || !source.isActiveAndEnabled || source.isTrigger) continue;
                for (int targetIndex = 0; targetIndex < targetColliders.Count; targetIndex++)
                {
                    Collider2D target = targetColliders[targetIndex];
                    if (!target || !target.isActiveAndEnabled || target.isTrigger) continue;

                    ColliderDistance2D distance = Physics2D.Distance(source, target);
                    if (distance.isValid)
                    {
                        closest = Mathf.Min(closest, Mathf.Max(0f, distance.distance));
                    }
                }
            }

            return closest;
        }
    }
}
