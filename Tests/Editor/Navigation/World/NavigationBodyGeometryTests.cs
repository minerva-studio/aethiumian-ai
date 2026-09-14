using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>Verifies project navigation anchors use the current Collider2D world AABB.</summary>
    public sealed class NavigationBodyGeometryTests
    {
        /// <summary>Verifies horizontal and vertical collider offsets affect both ground and center anchors.</summary>
        [Test]
        public void AnchorsComeFromOffsetColliderBounds()
        {
            GameObject gameObject = new("navigation-body-geometry");
            try
            {
                gameObject.transform.position = new Vector3(3f, 4f, 0f);
                BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.5f, 2f);
                collider.offset = new Vector2(0.35f, -0.2f);
                Physics2D.SyncTransforms();

                Bounds bounds = collider.bounds;
                Assert.That(NavigationBodyGeometry.GetGroundAnchor(collider), Is.EqualTo(new Vector2(bounds.center.x, bounds.min.y)));
                Assert.That(NavigationBodyGeometry.GetCenterAnchor(collider), Is.EqualTo((Vector2)bounds.center));
                Assert.That(NavigationBodyGeometry.GetWorldAabbSize(collider), Is.EqualTo((Vector2)bounds.size));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Verifies navigation geometry merges associated child colliders and excludes triggers.</summary>
        [Test]
        public void AssociatedCollidersMergeIntoOneAabbAndExcludeTriggers()
        {
            GameObject gameObject = new("navigation-body-geometry-aggregate");
            try
            {
                Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
                BoxCollider2D rootCollider = gameObject.AddComponent<BoxCollider2D>();
                rootCollider.size = new Vector2(2f, 2f);
                rootCollider.offset = Vector2.left;

                BoxCollider2D trigger = gameObject.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;

                GameObject child = new("navigation-body-geometry-child");
                child.transform.SetParent(gameObject.transform);
                child.transform.localPosition = new Vector3(3f, 0f, 0f);
                BoxCollider2D childCollider = child.AddComponent<BoxCollider2D>();
                childCollider.size = Vector2.one;
                Physics2D.SyncTransforms();

                Collider2D[] colliders = NavigationBodyGeometry.GetColliders(body);
                Bounds bounds = NavigationBodyGeometry.GetMergedBounds(colliders);

                Assert.That(colliders, Has.Length.EqualTo(2));
                Assert.That(bounds.min.x, Is.EqualTo(-2f).Within(0.0001f));
                Assert.That(bounds.max.x, Is.EqualTo(3.5f).Within(0.0001f));
                Vector2 groundAnchor = NavigationBodyGeometry.GetGroundAnchor(colliders);
                Assert.That(groundAnchor.x, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(groundAnchor.y, Is.EqualTo(-1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
