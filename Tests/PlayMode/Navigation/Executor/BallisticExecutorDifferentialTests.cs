using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies the analytical damped trajectory against a real Rigidbody2D.</summary>
    public sealed class BallisticExecutorDifferentialTests
    {
        private const float GravityScale = 4f;
        private const float LinearDamping = 5f;
        private const float PositionTolerance = 0.01f;
        private static readonly Vector2 PhysicsOrigin = new(-1000f, -1000f);
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

        /// <summary>Verifies fixed-tick analytical position and velocity against Rigidbody2D flight.</summary>
        [UnityTest]
        public IEnumerator MatchesRealRigidbodyFlight()
        {
            float timeStep = Time.fixedDeltaTime;
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            collider.offset = Vector2.up * 0.5f;
            BoxCollider2D floor = CreateBox("ballistic-support", new Vector2(1f, -0.5f), new Vector2(6f, 1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();
            Assert.That(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(Vector2.zero, new Vector2(2f, 0f), Physics2D.gravity,
                    GravityScale, LinearDamping, 2f, timeStep),
                out JumpTrajectorySolution solution), Is.True);
            using var executor = new BallisticJumpExecutor(body, collider, new[] { collider },
                new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, 0).CreateTerrainFilter(), solution, null);

            Assert.That(executor.Tick(timeStep).Status, Is.EqualTo(ExecutionStatus.Running));
            floor.enabled = false;
            AssertVector(body.linearVelocity, solution.InitialVelocity, "launch velocity");

            int flightTicks = Mathf.RoundToInt(solution.FlightDuration / timeStep);
            int apexTick = Mathf.RoundToInt(solution.ApexTime / timeStep);
            Assert.That(solution.FlightDuration, Is.EqualTo(flightTicks * timeStep).Within(0.000001f));
            Assert.That(solution.ApexTime, Is.EqualTo(apexTick * timeStep).Within(0.000001f));

            float previousVerticalVelocity = solution.InitialVelocity.y;
            Vector2 actualApex = default;
            bool executorCompleted = false;
            for (int tick = 1; tick <= flightTicks; tick++)
            {
                yield return new WaitForFixedUpdate();
                float elapsed = tick * timeStep;
                AssertVector(body.position, solution.GetPosition(elapsed), $"position at fixed tick {tick}");
                AssertVector(body.linearVelocity, solution.GetVelocity(elapsed), $"velocity at fixed tick {tick}");

                if (tick == apexTick)
                {
                    actualApex = body.position;
                    Assert.That(previousVerticalVelocity, Is.GreaterThan(0f));
                    Assert.That(body.linearVelocityY, Is.LessThanOrEqualTo(0f));
                }

                previousVerticalVelocity = body.linearVelocityY;
                if (tick == flightTicks)
                {
                    floor.enabled = true;
                    Physics2D.SyncTransforms();
                }

                executorCompleted = executor.Tick(timeStep).Status == ExecutionStatus.Completed;
                Assert.That(executorCompleted, Is.EqualTo(tick == flightTicks),
                    $"Executor completion did not match the landing tick {flightTicks}.");
            }

            AssertVector(actualApex, solution.ApexPosition, "sampled apex");
            AssertVector(body.position, solution.LandingPosition, "landing tick");
            Assert.That(executorCompleted, Is.True);
        }

        private (Rigidbody2D body, BoxCollider2D collider) CreateBody(Vector2 position)
        {
            GameObject host = new("ballistic-differential-body");
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

        private static void AssertVector(Vector2 actual, Vector2 expected, string stage)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(PositionTolerance), $"Unexpected x component for {stage}.");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(PositionTolerance), $"Unexpected y component for {stage}.");
        }
    }
}
