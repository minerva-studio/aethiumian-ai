using System;
using System.Collections.Generic;
using UnityEngine;
using Aethiumian.AI.Geometry;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Collects the enabled non-trigger Collider2D geometry used by movement navigation.</summary>
    public static class NavigationBodyGeometry
    {
        /// <summary>Gets all enabled non-trigger colliders attached to a Rigidbody2D hierarchy.</summary>
        public static Collider2D[] GetColliders(Rigidbody2D body)
            => ColliderGeometryQuery.GetColliders(body);

        /// <summary>Gets all enabled non-trigger colliders from a target hierarchy, preferring its Rigidbody2D association.</summary>
        public static Collider2D[] GetTargetColliders(GameObject target)
            => ColliderGeometryQuery.GetTargetColliders(target);

        /// <summary>Gets the merged world-space box of a non-empty collider collection.</summary>
        public static AABB GetMergedAabb(IReadOnlyList<Collider2D> colliders)
            => AABB.FromBounds(ColliderGeometryQuery.GetMergedBounds(colliders));

        /// <summary>Gets the lower-center ground anchor of a merged world-space box.</summary>
        public static Vector2 GetGroundAnchor(IReadOnlyList<Collider2D> colliders)
            => GetMergedAabb(colliders).LowerCenter;

        /// <summary>Gets the body center of a merged world-space box.</summary>
        public static Vector2 GetBodyCenter(IReadOnlyList<Collider2D> colliders)
            => GetMergedAabb(colliders).Center;

        /// <summary>Gets the size of a merged world-space box.</summary>
        public static Vector2 GetWorldAabbSize(IReadOnlyList<Collider2D> colliders)
            => GetMergedAabb(colliders).Size;

        /// <summary>Gets the lower-center ground anchor of one collider's current world box.</summary>
        public static Vector2 GetGroundAnchor(Collider2D collider)
            => GetAabb(collider).LowerCenter;

        /// <summary>Gets the body center of one collider's current world box.</summary>
        public static Vector2 GetBodyCenter(Collider2D collider)
            => GetAabb(collider).Center;

        /// <summary>Gets the size of one collider's current world box.</summary>
        public static Vector2 GetWorldAabbSize(Collider2D collider)
            => GetAabb(collider).Size;


        private static AABB GetAabb(Collider2D collider)
            => collider ? AABB.FromBounds(collider.bounds) : throw new ArgumentNullException(nameof(collider));
    }
}
