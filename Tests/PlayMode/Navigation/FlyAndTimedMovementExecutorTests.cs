using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>Verifies fixed-step physics behavior for aerial and bounded maneuver executors.</summary>
    public sealed class FlyAndTimedMovementExecutorTests
    {
        private readonly List<GameObject> createdObjects = new();

        /// <summary>Destroys every runtime physics fixture after a test.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index])
                {
                    Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        /// <summary>Verifies custom terrain candidates and each actor exclusion preserve aerial blocking.</summary>
        [TestCase(0, 0, true)]
        [TestCase(1, 0, false)]
        [TestCase(0, 1, false)]
        [TestCase(2, 2, true)]
        public void FlyTick_CustomTerrainRespectsExclusions(int bodyExclusions, int colliderExclusions, bool expectedBlocked)
        {
            Vector2 origin = new(-1200f, -1200f);
            Rigidbody2D body = CreateBody("custom-filter-fly", origin, out Collider2D collider);
            body.gameObject.layer = 2;
            body.excludeLayers = bodyExclusions;
            collider.excludeLayers = colliderExclusions;
            CreateWall("custom-filter-wall", origin + new Vector2(0.56f, 0f), new Vector2(0.1f, 4f));
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new NavigationPhysicsLayers(1, 0).CreateTerrainFilter();
            filter.SetLayerMask(filter.layerMask.value & ~body.excludeLayers.value & ~collider.excludeLayers.value);
            var executor = new FlyTraversalExecutor(body, collider, 4f, 1f, filter);
            executor.SetWaypoint(NavigationBodyGeometry.GetCenterAnchor(collider), origin + Vector2.right * 2f,
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);

            ExecutionResult result = executor.Tick(0.02f);
            Assert.That(result.Status, Is.EqualTo(expectedBlocked
                ? ExecutionStatus.Failed
                : ExecutionStatus.Running));
            Assert.That(result.FailureReason, Is.EqualTo(expectedBlocked
                ? ExecutionFailureReason.Obstructed
                : ExecutionFailureReason.None));
            Assert.That(body.linearVelocity.x, Is.EqualTo(expectedBlocked ? 0f : 4f).Within(0.0001f));
        }

        /// <summary>Verifies that aerial construction is inert and Tick applies bounded acceleration.</summary>
        [UnityTest]
        public IEnumerator FlyTick_IsTheOnlyVelocityWriteAndMovesThroughPhysics()
        {
            Rigidbody2D body = CreateBody("fly-body", Vector2.zero, out Collider2D bodyCollider);
            ContactFilter2D filter = new ContactFilter2D().NoFilter();
            var executor = new FlyTraversalExecutor(body, bodyCollider, 4f, 0.05f, filter);
            Vector2 steeringTarget = new Vector2(2f, 0f);

            Assert.AreEqual(Vector2.zero, body.linearVelocity, "Construction must not write physics state.");

            executor.SetWaypoint(NavigationBodyGeometry.GetCenterAnchor(bodyCollider), steeringTarget,
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            ExecutionResult result = executor.Tick(0.02f);

            Assert.AreEqual(ExecutionStatus.Running, result.Status);
            Assert.AreEqual(0.2f, body.linearVelocity.x, 0.0001f);
            Assert.AreEqual(0f, body.linearVelocity.y, 0.0001f);
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.position.x, 0f, "The real Rigidbody2D simulation must consume the submitted velocity.");
        }

        /// <summary>Verifies that an already reached fly step completes without clearing existing momentum.</summary>
        [Test]
        public void FlyTick_AtArrivalDoesNotWriteVelocity()
        {
            Rigidbody2D body = CreateBody("arrived-fly-body", Vector2.zero, out Collider2D bodyCollider);
            body.linearVelocity = new Vector2(3f, -2f);
            var executor = new FlyTraversalExecutor(
                body,
                bodyCollider,
                4f,
                0.1f,
                new ContactFilter2D().NoFilter());

            executor.SetWaypoint(NavigationBodyGeometry.GetCenterAnchor(bodyCollider), new Vector2(0.005f, 0f),
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            ExecutionResult result = executor.Tick(0.02f);

            Assert.AreEqual(ExecutionStatus.Completed, result.Status);
            Assert.AreEqual(new Vector2(3f, -2f), body.linearVelocity);
        }

        /// <summary>Verifies aerial arrival uses the Collider2D center rather than Rigidbody2D position.</summary>
        [Test]
        public void FlyTick_AtOffsetColliderCenterDoesNotWriteVelocity()
        {
            Rigidbody2D body = CreateBody("offset-fly-body", Vector2.zero, out Collider2D bodyCollider);
            ((BoxCollider2D)bodyCollider).offset = new Vector2(0.75f, -0.25f);
            Physics2D.SyncTransforms();
            body.linearVelocity = new Vector2(3f, -2f);
            var executor = new FlyTraversalExecutor(
                body,
                bodyCollider,
                4f,
                0.1f,
                new ContactFilter2D().NoFilter());
            Vector2 center = NavigationBodyGeometry.GetCenterAnchor(bodyCollider);

            executor.SetWaypoint(center, center + Vector2.right * 0.005f,
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            ExecutionResult result = executor.Tick(0.02f);

            Assert.AreEqual(ExecutionStatus.Completed, result.Status);
            Assert.AreEqual(new Vector2(3f, -2f), body.linearVelocity);
        }

        /// <summary>Verifies target updates preserve the prior velocity used by aerial smoothing.</summary>
        [Test]
        public void FlyTick_UpdatingTargetPreservesSmoothingHistory()
        {
            Rigidbody2D body = CreateBody("retarget-fly-body", Vector2.zero, out Collider2D bodyCollider);
            var executor = new FlyTraversalExecutor(
                body,
                bodyCollider,
                4f,
                0.5f,
                new ContactFilter2D().NoFilter());

            executor.SetWaypoint(NavigationBodyGeometry.GetCenterAnchor(bodyCollider), new Vector2(2f, 0f),
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            Assert.AreEqual(ExecutionStatus.Running, executor.Tick(0.02f).Status);
            Assert.AreEqual(2f, body.linearVelocity.x, 0.0001f);

            executor.SetWaypoint(NavigationBodyGeometry.GetCenterAnchor(bodyCollider), new Vector2(0f, 2f),
                Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            Assert.AreEqual(ExecutionStatus.Running, executor.Tick(0.02f).Status);
            Assert.AreEqual(1f, body.linearVelocity.x, 0.0001f);
            Assert.AreEqual(2f, body.linearVelocity.y, 0.0001f);
        }

        /// <summary>Verifies the shared visibility query observes a circular body and an L-shaped terrain corner.</summary>
        [Test]
        public void TargetVisibilityQuery_LShapedCornerBlocksAndThenReleasesSight()
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
        public void TargetVisibilityQuery_UsesSelectedDistanceMeasurement()
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

        /// <summary>Verifies the fixed-tick response coefficient rejects values outside its authored contract.</summary>
        [Test]
        public void FlyExecutor_RejectsFlexibilityOutsideUnitInterval()
        {
            Rigidbody2D body = CreateBody("invalid-flexibility-body", Vector2.zero, out Collider2D bodyCollider);

            Assert.That(() =>
                new FlyTraversalExecutor(body, bodyCollider, 4f, 1.01f, new ContactFilter2D().NoFilter()),
                Throws.InstanceOf<System.ArgumentException>());
        }

        /// <summary>Verifies that a bounded force maneuver writes once per active Tick and not during construction.</summary>
        [Test]
        public void TimedForceTick_AppliesContinuousImpulseForItsDuration()
        {
            Rigidbody2D body = CreateBody("timed-force-body", Vector2.zero, out _);
            body.mass = 2f;
            var executor = new TimedForceExecutor(body, new Vector2(2f, 0f), ForceMode2D.Impulse, 0.04f);

            Assert.AreEqual(Vector2.zero, body.linearVelocity, "Construction must not apply the force.");

            Assert.AreEqual(ExecutionStatus.Running, executor.Tick(0.02f).Status);
            Assert.AreEqual(1f, body.linearVelocity.x, 0.0001f);
            Assert.AreEqual(ExecutionStatus.Completed, executor.Tick(0.02f).Status);
            Assert.AreEqual(2f, body.linearVelocity.x, 0.0001f);
            Assert.Throws<System.InvalidOperationException>(() => executor.Tick(0.02f));
            Assert.AreEqual(2f, body.linearVelocity.x, 0.0001f, "Completed ticks must not submit additional force.");
        }

        /// <summary>Verifies a zero-duration force action completes without applying a force.</summary>
        [Test]
        public void TimedForceTick_ZeroDurationDoesNotApplyForce()
        {
            Rigidbody2D body = CreateBody("zero-duration-force-body", Vector2.zero, out _);
            var executor = new TimedForceExecutor(body, new Vector2(2f, 0f), ForceMode2D.Impulse, 0f);

            Assert.AreEqual(ExecutionStatus.Completed, executor.Tick(0.02f).Status);
            Assert.AreEqual(Vector2.zero, body.linearVelocity);
        }

        /// <summary>Verifies that a ballistic maneuver uses the trajectory launch and duration as one source of truth.</summary>
        [Test]
        public void BallisticJumpTick_RequiresLandingAfterTrajectoryDuration()
        {
            var input = new JumpTrajectoryInput(
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0f, -9.81f),
                1f,
                0f,
                1f,
                0.02f);
            Assert.IsTrue(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution trajectory));

            Rigidbody2D body = CreateBody("ballistic-body", Vector2.up * 0.5f, out Collider2D collider);
            var floor = new GameObject("ballistic-support");
            createdObjects.Add(floor);
            floor.transform.position = new Vector2(1f, -0.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(6f, 1f);
            Physics2D.SyncTransforms();
            body.mass = 3f;
            body.linearVelocity = new Vector2(-1f, -2f);
            using var executor = new BallisticJumpExecutor(body, collider, new[] { collider }, new ContactFilter2D { useLayerMask = false, useTriggers = false }, trajectory, null);

            Assert.AreEqual(new Vector2(-1f, -2f), body.linearVelocity, "Construction must not launch the body.");
            Assert.That(executor.Tick(trajectory.FlightDuration * 0.5f).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.AreEqual(trajectory.InitialVelocity.x, body.linearVelocity.x, 0.0001f);
            Assert.AreEqual(trajectory.InitialVelocity.y, body.linearVelocity.y, 0.0001f);

            Vector2 launchedVelocity = body.linearVelocity;
            Assert.That(executor.Tick(trajectory.FlightDuration * 0.5f).Status, Is.EqualTo(ExecutionStatus.Running));
            body.position = trajectory.LandingPosition + Vector2.up * 0.5f;
            Physics2D.SyncTransforms();
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Completed));
            Assert.AreEqual(launchedVelocity, body.linearVelocity, "Only the first ballistic Tick may submit the launch impulse.");
        }

        /// <summary>Creates one dynamic Rigidbody2D and its cached body collider.</summary>
        private Rigidbody2D CreateBody(string name, Vector2 position, out Collider2D bodyCollider)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            gameObject.transform.position = position;
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            bodyCollider = gameObject.AddComponent<BoxCollider2D>();
            return body;
        }

        /// <summary>Creates a circle-bodied fixture matching the Rhombear navigation collider shape.</summary>
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

        /// <summary>Creates a static layer-zero wall fixture for visibility-query tests.</summary>
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
