using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies Ballistic executor support-filter ownership without planner integration.</summary>
    public sealed class BallisticExecutorContractTests
    {
        private const float GravityScale = 4f;
        private const float LinearDamping = 5f;
        private const float PositionTolerance = 0.01f;
        private static readonly Vector2 PhysicsOrigin = new(-1200f, -1200f);
        private readonly List<GameObject> objects = new();
        private bool geometryCollisionWasIgnored;

        [SetUp]
        public void SetUp()
        {
            geometryCollisionWasIgnored = Physics2D.GetIgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                geometryCollisionWasIgnored);
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index])
                    Object.Destroy(objects[index]);
            }

            objects.Clear();
            yield return null;
        }

        /// <summary>Verifies an explicit terrain filter controls support resolution and launch writes.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void UsesSuppliedSupportFilter(bool includeSupport)
        {
            const int supportLayer = 0;
            CreateBox("custom-support", PhysicsOrigin + new Vector2(0f, -0.5f), new Vector2(4f, 1f), supportLayer);
            (Rigidbody2D body, Collider2D collider) = CreateBody(PhysicsOrigin + new Vector2(0f, 0.51f));
            body.gameObject.layer = 2;
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new NavigationPhysicsLayers(includeSupport ? 1 << supportLayer : 0, 0).CreateTerrainFilter();

            Assert.That(NavigationWorldQueries.TryGetGroundSupportPoint(collider, filter, out Vector2 support), Is.EqualTo(includeSupport));
            if (includeSupport)
                Assert.That(support.y, Is.EqualTo(PhysicsOrigin.y).Within(PositionTolerance));

            Vector2 start = new(PhysicsOrigin.x, PhysicsOrigin.y);
            Assert.That(JumpTrajectory.TrySolve(new JumpTrajectoryInput(start, start + Vector2.right,
                Physics2D.gravity, GravityScale, LinearDamping, 2f, Time.fixedDeltaTime), out JumpTrajectorySolution trajectory), Is.True);
            using var executor = new BallisticJumpExecutor(body, collider, new[] { collider }, filter, trajectory, null);

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            if (includeSupport)
                AssertVector(body.linearVelocity, trajectory.InitialVelocity, PositionTolerance);
            else
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero), "Excluded support must not permit launch.");
        }

        private (Rigidbody2D body, Collider2D collider) CreateBody(Vector2 position)
        {
            GameObject host = new("ballistic-filter-body");
            objects.Add(host);
            host.transform.position = position;
            Rigidbody2D body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = GravityScale;
            body.linearDamping = LinearDamping;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            return (body, host.AddComponent<BoxCollider2D>());
        }

        private BoxCollider2D CreateBox(string name, Vector2 position, Vector2 size, int layer)
        {
            GameObject host = new(name) { layer = layer };
            objects.Add(host);
            host.transform.position = position;
            BoxCollider2D collider = host.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
        }
    }
}
