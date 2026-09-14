using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies target visibility geometry independently of movement executors.</summary>
    public sealed class NavigationVisibilityQueryTests
    {
        private readonly List<GameObject> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index])
                    Object.Destroy(createdObjects[index]);
            }

            createdObjects.Clear();
            yield return null;
        }

        /// <summary>Verifies the visibility query observes an L-shaped terrain corner and its removal.</summary>
        [Test]
        public void LShapedCornerBlocksAndThenReleasesSight()
        {
            GameObject source = CreateCircleBody("visibility-source", Vector2.zero, out Collider2D sourceCollider).gameObject;
            source.layer = 2;
            GameObject target = CreateCircleBody("visibility-target", new Vector2(3f, 3f), out Collider2D targetCollider).gameObject;
            target.layer = 2;

            GameObject verticalWall = CreateWall("visibility-vertical-wall", new Vector2(1.5f, 1.5f), new Vector2(0.2f, 3.4f));
            GameObject horizontalWall = CreateWall("visibility-horizontal-wall", new Vector2(2.3f, 1.7f), new Vector2(1.8f, 0.2f));
            Physics2D.SyncTransforms();

            LayerMask blockingLayers = 1 << 0;
            Assert.IsFalse(TargetVisibilityQuery.IsVisible(
                source.transform.position,
                target.transform.position,
                sourceCollider,
                targetCollider,
                blockingLayers));

            verticalWall.SetActive(false);
            horizontalWall.SetActive(false);
            Physics2D.SyncTransforms();

            Assert.IsTrue(TargetVisibilityQuery.IsVisible(
                source.transform.position,
                target.transform.position,
                sourceCollider,
                targetCollider,
                blockingLayers));
        }

        /// <summary>Verifies visibility range uses the selected existing DistanceTo geometry contract.</summary>
        [Test]
        public void UsesSelectedDistanceMeasurement()
        {
            Rigidbody2D sourceBody = CreateCircleBody("measurement-source", Vector2.zero, out Collider2D sourceCollider);
            Rigidbody2D targetBody = CreateCircleBody("measurement-target", new Vector2(5.5f, 5.5f), out Collider2D targetCollider);
            Physics2D.SyncTransforms();

            LayerMask noBlockingLayers = 0;
            const float maxDistance = 7f;
            Assert.IsFalse(TargetVisibilityQuery.IsVisible(
                sourceBody.position,
                targetBody.position,
                sourceCollider,
                targetCollider,
                noBlockingLayers,
                maxDistance,
                DistanceTo.Measurement.TransformPosition));
            Assert.IsTrue(TargetVisibilityQuery.IsVisible(
                sourceBody.position,
                targetBody.position,
                sourceCollider,
                targetCollider,
                noBlockingLayers,
                maxDistance,
                DistanceTo.Measurement.ColliderBounds));
            Assert.IsFalse(TargetVisibilityQuery.IsVisible(
                sourceBody.position,
                targetBody.position,
                sourceCollider,
                targetCollider,
                noBlockingLayers,
                maxDistance,
                DistanceTo.Measurement.ColliderSurface));
        }

        private Rigidbody2D CreateCircleBody(string name, Vector2 position, out Collider2D bodyCollider)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            gameObject.transform.position = position;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.33366773f;
            bodyCollider = circle;
            return body;
        }

        private GameObject CreateWall(string name, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            gameObject.transform.position = position;
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            return gameObject;
        }
    }
}
