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

        /// <summary>Verifies a ballistic launch writes once and completes after fixed-step landing support is observed.</summary>
        [UnityTest]
        public IEnumerator BallisticExecutor_WritesOnlyOnFirstTick()
        {
            const float timeStep = 0.02f;
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 1f, 0.02f);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            var host = new GameObject("ballistic-executor-test");
            var floor = new GameObject("ballistic-executor-floor");

            try
            {
                Rigidbody2D body = host.AddComponent<Rigidbody2D>();
                body.gravityScale = 1f;
                body.linearDamping = 0f;
                body.mass = 2f;
                body.linearVelocity = new Vector2(-1f, -2f);
                BoxCollider2D collider = host.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
                body.position = Vector2.up * 0.5f;
                floor.transform.position = new Vector2(0f, -0.5f);
                BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
                floorCollider.size = new Vector2(4f, 1f);
                Physics2D.SyncTransforms();
                ContactFilter2D supportFilter = new() { useLayerMask = false, useTriggers = false };
                using var executor = new BallisticJumpExecutor(
                    body, collider, new[] { collider }, supportFilter, solution, null);

                AssertVector(body.linearVelocity, new Vector2(-1f, -2f), 0.0001f);
                Assert.That(executor.Tick(timeStep).Status, Is.EqualTo(ExecutionStatus.Running));
                AssertVector(body.linearVelocity, solution.InitialVelocity, 0.0001f);
                floorCollider.enabled = false;
                Physics2D.SyncTransforms();
                int flightTicks = Mathf.RoundToInt(solution.FlightDuration / timeStep);
                for (int tick = 1; tick < flightTicks; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    Vector2 velocityBeforeTick = body.linearVelocity;
                    Assert.That(executor.Tick(timeStep).Status, Is.EqualTo(ExecutionStatus.Running));
                    AssertVector(body.linearVelocity, velocityBeforeTick, 0.0001f);
                }

                yield return new WaitForFixedUpdate();
                floorCollider.enabled = true;
                Physics2D.SyncTransforms();
                ExecutionResult terminal = ExecutionResult.Running;
                for (int tick = 0; tick < 8 && terminal.Status == ExecutionStatus.Running; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    Vector2 velocityBeforeLandingTick = body.linearVelocity;
                    terminal = executor.Tick(timeStep);
                    AssertVector(body.linearVelocity, velocityBeforeLandingTick, 0.0001f);
                }

                Assert.That(terminal.Status, Is.EqualTo(ExecutionStatus.Completed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(floor);
            }
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
