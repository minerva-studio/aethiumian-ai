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

        /// <summary>Gets the merged world-space AABB of a non-empty collider collection.</summary>
        public static Bounds GetMergedBounds(IReadOnlyList<Collider2D> colliders)
            => ColliderGeometryQuery.GetMergedBounds(colliders);

        /// <summary>Gets the lower-center anchor of a merged world-space AABB.</summary>
        public static Vector2 GetGroundAnchor(IReadOnlyList<Collider2D> colliders)
        {
            Bounds bounds = GetMergedBounds(colliders);
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        /// <summary>Gets the center anchor of a merged world-space AABB.</summary>
        public static Vector2 GetCenterAnchor(IReadOnlyList<Collider2D> colliders)
            => GetMergedBounds(colliders).center;

        /// <summary>Gets the size of a merged world-space AABB.</summary>
        public static Vector2 GetWorldAabbSize(IReadOnlyList<Collider2D> colliders)
            => GetMergedBounds(colliders).size;

        /// <summary>Gets the lower-center anchor of one collider's current world AABB.</summary>
        public static Vector2 GetGroundAnchor(Collider2D collider)
        {
            Bounds bounds = GetBounds(collider);
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        /// <summary>Gets the center anchor of one collider's current world AABB.</summary>
        public static Vector2 GetCenterAnchor(Collider2D collider)
            => GetBounds(collider).center;

        /// <summary>Gets the size of one collider's current world AABB.</summary>
        public static Vector2 GetWorldAabbSize(Collider2D collider)
            => GetBounds(collider).size;


        private static Bounds GetBounds(Collider2D collider)
            => collider ? collider.bounds : throw new ArgumentNullException(nameof(collider));
    }
}
