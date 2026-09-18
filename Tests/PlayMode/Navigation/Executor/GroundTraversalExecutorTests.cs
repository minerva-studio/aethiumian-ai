using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Verifies fixed-step physics and collision cleanup for ground traversal execution.
    /// </summary>
    public sealed class GroundTraversalExecutorTests
    {
        private GameObject bodyObject;
        private GameObject floorObject;
        private bool geometryCollisionWasIgnored;
        private bool platformCollisionWasIgnored;

        [SetUp]
        public void SetUp()
        {
            geometryCollisionWasIgnored = Physics2D.GetIgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer);
            platformCollisionWasIgnored = Physics2D.GetIgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                false);
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.PlatformLayer,
                false);
        }

        /// <summary>Destroys the isolated runtime physics fixture after each test.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                geometryCollisionWasIgnored);
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.PlatformLayer,
                platformCollisionWasIgnored);
            if (bodyObject) Object.Destroy(bodyObject);
            if (floorObject) Object.Destroy(floorObject);
            yield return null;
        }

        /// <summary>Verifies that beginning a walk is inert and Tick changes only horizontal velocity.</summary>
        [Test]
        public void GroundMove_WritesOnlyWhenTickedAndPreservesVerticalVelocity()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            CreateFloor(new Vector2(0f, -0.56f), new Vector2(4f, 0.1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();
            body.linearVelocity = new Vector2(0f, 3f);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 0.5f);
            executor.SetGroundMove(new Vector2(0f, -0.5f), new Vector2(2f, -0.5f));

            Assert.AreEqual(new Vector2(0f, 3f), body.linearVelocity);

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.AreEqual(2f, body.linearVelocityX, 0.0001f);
            Assert.AreEqual(3f, body.linearVelocityY, 0.0001f);
        }

        /// <summary>Verifies ground movement consumes the supplied terrain filter instead of project layer defaults.</summary>
        [Test]
        public void GroundMove_UsesSuppliedTerrainFilter()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            CreateFloor(new Vector2(0f, -0.56f), new Vector2(4f, 0.1f), NavigationPhysicsTestLayers.DefaultLayer);
            Physics2D.SyncTransforms();
            Vector2 anchor = NavigationBodyGeometry.GetGroundAnchor(collider);

            ContactFilter2D includedFilter = new NavigationPhysicsLayers(NavigationPhysicsTestLayers.DefaultMask, 0).CreateTerrainFilter();
            using (var executor = new GroundTraversalExecutor(body, collider, includedFilter, 4f, 1f))
            {
                executor.SetGroundMove(anchor, anchor + Vector2.right * 2f);
                Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            }

            body.linearVelocity = Vector2.zero;
            ContactFilter2D excludedFilter = new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, 0).CreateTerrainFilter();
            using var excludedExecutor = new GroundTraversalExecutor(body, collider, excludedFilter, 4f, 1f);
            excludedExecutor.SetGroundMove(anchor, anchor + Vector2.right * 2f);
            ExecutionResult blockedResult = excludedExecutor.Tick(Time.fixedDeltaTime);
            Assert.That(blockedResult.Status, Is.EqualTo(ExecutionStatus.Failed));
            Assert.That(blockedResult.FailureReason, Is.EqualTo(ExecutionFailureReason.Obstructed));
        }

        /// <summary>Verifies a ground move cannot report complete while the body is still short of its endpoint.</summary>
        [TestCase("centered", 0f, 0f)]
        [TestCase("offset", 0.75f, -0.25f)]
        public void GroundMove_DoesNotCompleteWhileShortOfItsEndpoint(
            string caseName, float colliderOffsetX, float colliderOffsetY)
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            collider.offset = new Vector2(colliderOffsetX, colliderOffsetY);
            CreateFloor(new Vector2(0f, -0.56f), new Vector2(4f, 0.1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();
            Vector2 groundAnchor = NavigationBodyGeometry.GetGroundAnchor(collider);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 0.5f);
            executor.SetGroundMove(groundAnchor, groundAnchor + Vector2.right * 0.2f);

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running),
                "Arrival is directional: the move may only complete once the body has reached its endpoint.");
        }

        /// <summary>Verifies a sub-tick ground connector can still travel and complete.</summary>
        [UnityTest]
        public IEnumerator GroundMove_CompletesAfterTravellingASubTickConnector()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            CreateFloor(new Vector2(0f, -0.56f), new Vector2(4f, 0.1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Vector2 groundAnchor = NavigationBodyGeometry.GetGroundAnchor(collider);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 0.5f);
            executor.SetGroundMove(groundAnchor, groundAnchor + Vector2.right * 0.03f);

            ExecutionStatus status = ExecutionStatus.Running;
            for (int tick = 0; tick < 30 && status == ExecutionStatus.Running; tick++)
            {
                yield return new WaitForFixedUpdate();
                status = executor.Tick(Time.fixedDeltaTime).Status;
            }

            Assert.That(status, Is.EqualTo(ExecutionStatus.Completed),
                "A sub-tick connector must still be able to travel and complete.");
            Assert.That(NavigationBodyGeometry.GetGroundAnchor(collider).x,
                Is.GreaterThanOrEqualTo(groundAnchor.x + 0.03f - 0.0001f),
                "Completion requires actually reaching the endpoint, so the anchor must have moved.");
        }
        /// <summary>Verifies that a jump applies its shared launch solution exactly once from Tick.</summary>
        [UnityTest]
        public IEnumerator Jump_LaunchesOnceFromTick()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            CreateFloor(new Vector2(0f, -0.55f), new Vector2(4f, 0.1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(new Vector2(0f, -0.5f), new Vector2(1f, -0.5f), Physics2D.gravity, 1f, 0f, 1f, Time.fixedDeltaTime),
                out JumpTrajectorySolution solution));
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            executor.BeginJump(solution);

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
        }

        /// <summary>Verifies a damped jump leaves the floor, lands once, and completes against the observed contact gap.</summary>
        [UnityTest]
        public IEnumerator Jump_DampedBodyLeavesGroundAndCompletesAfterLanding()
        {
            const float GravityScale = 4f;
            const float LinearDamping = 5f;
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(new Vector2(0f, 2f));
            body.gravityScale = GravityScale;
            body.linearDamping = LinearDamping;
            collider.size = new Vector2(0.8f, 1.5f);
            CreateFloor(new Vector2(1f, -0.5f), new Vector2(8f, 1f), NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.SyncTransforms();

            for (int tick = 0; tick < 120 && !body.IsTouchingLayers(NavigationPhysicsTestLayers.TerrainMask); tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.IsTouchingLayers(NavigationPhysicsTestLayers.TerrainMask), Is.True, "The body did not naturally settle onto the floor.");
            Assert.That(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(Vector2.zero, new Vector2(2f, 0f), Physics2D.gravity,
                    GravityScale, LinearDamping, 2f, Time.fixedDeltaTime),
                4096,
                1f,
                out JumpTrajectorySolution solution), Is.True);

            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            executor.BeginJump(solution);

            float settledAnchorY = NavigationBodyGeometry.GetGroundAnchor(collider).y;
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));

            int flightTicks = Mathf.RoundToInt(solution.FlightDuration / Time.fixedDeltaTime);
            bool leftGround = false;
            bool landed = false;
            ExecutionStatus completed = ExecutionStatus.Running;
            float maximumAnchorY = settledAnchorY;
            for (int tick = 1; tick <= flightTicks + 30; tick++)
            {
                yield return new WaitForFixedUpdate();
                Vector2 anchor = NavigationBodyGeometry.GetGroundAnchor(collider);
                maximumAnchorY = Mathf.Max(maximumAnchorY, anchor.y);
                leftGround |= anchor.y > settledAnchorY + 0.02f;
                if (tick == 5)
                {
                    Assert.That(leftGround, Is.True, "The jump did not leave the floor within five fixed ticks.");
                }

                landed |= leftGround && body.IsTouchingLayers(NavigationPhysicsTestLayers.TerrainMask);
                completed = executor.Tick(Time.fixedDeltaTime).Status;
                if (completed != ExecutionStatus.Running) break;
            }

            Vector2 finalAnchor = NavigationBodyGeometry.GetGroundAnchor(collider);
            Assert.That(leftGround, Is.True);
            Assert.That(maximumAnchorY, Is.GreaterThan(settledAnchorY + 0.5f));
            Assert.That(landed, Is.True, "The body did not return to the LevelGeometry floor.");
            Assert.That(completed, Is.EqualTo(ExecutionStatus.Completed),
                $"The executor did not complete within the planned duration plus 30 fixed ticks. "
                + $"Anchor=({finalAnchor.x:R}, {finalAnchor.y:R}), "
                + $"Landing=({solution.LandingPosition.x:R}, {solution.LandingPosition.y:R}), "
                + $"Delta=({Mathf.Abs(finalAnchor.x - solution.LandingPosition.x):R}, "
                + $"{Mathf.Abs(finalAnchor.y - solution.LandingPosition.y):R}), "
                + $"Distance={Vector2.Distance(finalAnchor, solution.LandingPosition)}, "
                + $"TouchingFloor={body.IsTouchingLayers(NavigationPhysicsTestLayers.TerrainMask)}.");
            Assert.That(Mathf.Abs(GetSupportSurfaceY(collider) - solution.LandingPosition.y),
                Is.LessThanOrEqualTo(Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon));
            Assert.That(Mathf.Abs(finalAnchor.x - solution.LandingPosition.x),
                Is.LessThanOrEqualTo(Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon));
        }

        /// <summary>Verifies a downward jump ignores only its one-way launch platform and restores the pair after landing.</summary>
        [UnityTest]
        public IEnumerator Jump_DownwardOneWayLaunchCrossesOnlyLaunchPlatform()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D launchPlatform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Collider2D lowerSupport = CreateChildFloor(new Vector2(0f, -2.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.GeometryLayer);
            Collider2D otherPlatform = CreateAdditionalPlatform(new Vector2(3f, -0.55f));
            body.gravityScale = 1f;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(new Vector2(0f, -0.5f), new Vector2(0f, -2.5f),
                    Physics2D.gravity, 1f, 0f, 1f, Time.fixedDeltaTime),
                out JumpTrajectorySolution solution), Is.True);

            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            JumpRouteSegment segment = new(solution.StartPosition, solution.LandingPosition, 1f,
                new[]
                {
                    new JumpSurfaceCrossing(new NavigationSurfaceId(0, 0), new Vector2(0f, -0.5f), Vector2.up, 0f, JumpSurfaceCrossingKind.Descending),
                });
            Assert.That(OneWayPlatformCollisionLease.TryCreateForSegment(collider, segment,
                new Dictionary<NavigationSurfaceId, Collider2D> { [new NavigationSurfaceId(0, 0)] = launchPlatform },
                out OneWayPlatformCollisionLease lease), Is.True);
            executor.BeginJump(solution, lease);
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(collider, otherPlatform), Is.False);

            ExecutionStatus completed = ExecutionStatus.Running;
            bool roseAboveLaunch = false;
            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                roseAboveLaunch |= collider.bounds.max.y > -0.45f;
                completed = executor.Tick(Time.fixedDeltaTime).Status;
                if (completed != ExecutionStatus.Running) break;
            }

            Assert.That(roseAboveLaunch, Is.True);
            Assert.That(completed, Is.EqualTo(ExecutionStatus.Completed),
                $"The downward jump did not land on the lower support in bounded fixed ticks. Status={completed}");
            Assert.That(body.IsTouchingLayers(NavigationPhysicsTestLayers.GeometryMask), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.False);
            Assert.That(Physics2D.GetIgnoreCollision(collider, otherPlatform), Is.False);
            Assert.That(lowerSupport, Is.Not.Null);
        }

        /// <summary>Reports an actual intermediate support instead of completing at the planned landing.</summary>
        [UnityTest]
        public IEnumerator Jump_UnexpectedSupportReturnsPhysicalResult()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D launchPlatform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            CreateChildFloor(new Vector2(0f, 0.75f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(new Vector2(0f, -0.5f), new Vector2(0f, -2.5f),
                    Physics2D.gravity, 1f, 0f, 2f, Time.fixedDeltaTime),
                out JumpTrajectorySolution solution), Is.True);
            JumpRouteSegment segment = new(solution.StartPosition, solution.LandingPosition, 2f,
                new[] { new JumpSurfaceCrossing(new NavigationSurfaceId(0, 0), new Vector2(0f, -0.5f), Vector2.up, 0f, JumpSurfaceCrossingKind.Descending) });
            Assert.That(OneWayPlatformCollisionLease.TryCreateForSegment(collider, segment,
                new Dictionary<NavigationSurfaceId, Collider2D> { [new NavigationSurfaceId(0, 0)] = launchPlatform },
                out OneWayPlatformCollisionLease lease), Is.True);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            executor.BeginJump(solution, lease);
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.True);

            body.position = new Vector2(0f, 1.31f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            ExecutionResult result = executor.Tick(
                solution.FlightDuration + Time.fixedDeltaTime);

            Assert.That(result.Status, Is.EqualTo(ExecutionStatus.Failed));
            Assert.That(result.FailureReason, Is.EqualTo(ExecutionFailureReason.UnexpectedSupport));
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.False);
        }

        /// <summary>Verifies that a contact-gap-sized vertical delta does not disable the same launch platform.</summary>
        [UnityTest]
        public IEnumerator Jump_SameLevelContactGapKeepsOneWayLaunchCollision()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D launchPlatform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            float launchY = -0.5f + Physics2D.defaultContactOffset * 0.5f;
            Assert.That(
                JumpTrajectory.TrySolve(
                    new JumpTrajectoryInput(
                        new Vector2(0f, launchY),
                        new Vector2(0.5f, -0.5f),
                        Physics2D.gravity,
                        1f,
                        0f,
                        1f,
                        Time.fixedDeltaTime),
                    out JumpTrajectorySolution solution),
                Is.True);

            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            executor.BeginJump(solution);

            Assert.That(solution.StartPosition.y - solution.LandingPosition.y,
                Is.LessThan(NavigationWorldQueries.SupportSnapDistance));
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.False);
        }

        /// <summary>Verifies a jump leases an intermediate platform and restores it after passing below.</summary>
        [UnityTest]
        public IEnumerator Jump_IntermediateOneWayPlatformIsIgnoredThenRestored()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D launchPlatform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Collider2D intermediatePlatform = CreateChildFloor(new Vector2(0f, 0.8f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Collider2D lowerSupport = CreateChildFloor(new Vector2(0f, -2.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            body.gravityScale = 1f;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(new Vector2(0f, -0.5f), new Vector2(0f, -2.5f),
                    Physics2D.gravity, 1f, 0f, 3f, Time.fixedDeltaTime),
                4096,
                1.5f,
                out JumpTrajectorySolution solution), Is.True);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            JumpRouteSegment segment = new(solution.StartPosition, solution.LandingPosition, 1.5f,
                new[]
                {
                    new JumpSurfaceCrossing(new NavigationSurfaceId(0, 0), new Vector2(0f, -0.5f), Vector2.up, 0f, JumpSurfaceCrossingKind.Descending),
                    new JumpSurfaceCrossing(new NavigationSurfaceId(1, 0), new Vector2(0f, 0.85f), Vector2.up, 0f, JumpSurfaceCrossingKind.Ascending),
                    new JumpSurfaceCrossing(new NavigationSurfaceId(1, 0), new Vector2(0f, 0.85f), Vector2.up, 0f, JumpSurfaceCrossingKind.Descending),
                    new JumpSurfaceCrossing(new NavigationSurfaceId(2, 0), new Vector2(0f, -2.5f), Vector2.up, 0f, JumpSurfaceCrossingKind.Landing),
                });
            Assert.That(OneWayPlatformCollisionLease.TryCreateForSegment(collider, segment,
                new Dictionary<NavigationSurfaceId, Collider2D>
                {
                    [new NavigationSurfaceId(0, 0)] = launchPlatform,
                    [new NavigationSurfaceId(1, 0)] = intermediatePlatform,
                    [new NavigationSurfaceId(2, 0)] = lowerSupport,
                },
                out OneWayPlatformCollisionLease lease), Is.True);
            executor.BeginJump(solution, lease);
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(Physics2D.GetIgnoreCollision(collider, intermediatePlatform), Is.True,
                $"Apex={solution.ApexPosition.y:R}, platformTop={intermediatePlatform.bounds.max.y:R}, "
                + $"body={collider.bounds}, layer={intermediatePlatform.gameObject.layer}");
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(collider, lowerSupport), Is.True);

            ExecutionStatus completed = ExecutionStatus.Running;
            for (int tick = 0; tick < 240; tick++)
            {
                yield return new WaitForFixedUpdate();
                completed = executor.Tick(Time.fixedDeltaTime).Status;
                if (completed != ExecutionStatus.Running) break;
            }

            Assert.That(completed, Is.EqualTo(ExecutionStatus.Completed),
                $"Unexpected execution status={completed}");
            Assert.That(Physics2D.GetIgnoreCollision(collider, intermediatePlatform), Is.False);
            Assert.That(Physics2D.GetIgnoreCollision(collider, launchPlatform), Is.False);
            Assert.That(lowerSupport, Is.Not.Null);
        }

        /// <summary>Verifies an obstructed ground action ends immediately and releases its execution.</summary>
        [UnityTest]
        public IEnumerator GroundMove_ObstructedActionEndsImmediately()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Physics2D.SyncTransforms();
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            Vector2 start = NavigationBodyGeometry.GetGroundAnchor(collider);
            executor.SetGroundMove(start, start + Vector2.right * 1f);

            ExecutionResult blockedResult = executor.Tick(Time.fixedDeltaTime);
            Assert.That(blockedResult.Status, Is.EqualTo(ExecutionStatus.Failed));
            Assert.That(blockedResult.FailureReason, Is.EqualTo(ExecutionFailureReason.Obstructed));

            Assert.Throws<System.InvalidOperationException>(() => executor.Tick(Time.fixedDeltaTime));
            yield return null;
        }

        /// <summary>Verifies replacing a drop-through action restores its lease before the next action starts.</summary>
        [UnityTest]
        public IEnumerator BeginAction_ReleasesPreviousPlatformLeaseBeforeReplacement()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D platform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(Physics2D.GetIgnoreCollision(collider, platform), Is.True);

            executor.SetGroundMove(new Vector2(0f, -0.5f), new Vector2(1f, -0.5f));

            Assert.That(Physics2D.GetIgnoreCollision(collider, platform), Is.False);
        }

        /// <summary>Verifies that falling stops submitting horizontal velocity after the ledge exit.</summary>
        [Test]
        public void Fall_AfterLedgeExitDoesNotWriteVelocity()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(new Vector2(1f, 0f));
            body.linearVelocity = new Vector2(1.25f, -2f);
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 4f, 1f);
            executor.BeginFall(new Vector2(0f, -0.5f), new Vector2(1f, -0.5f), new Vector2(1f, -2.5f));

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.AreEqual(new Vector2(1.25f, -2f), body.linearVelocity);
        }

        /// <summary>Verifies that drop-through ignores only the selected platform and Dispose restores that pair.</summary>
        [UnityTest]
        public IEnumerator DropThrough_DisposeRestoresExactColliderPair()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D platform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Collider2D otherPlatform = CreateAdditionalPlatform(new Vector2(3f, -0.55f));
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));

            Assert.IsFalse(Physics2D.GetIgnoreCollision(collider, platform));
            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.IsTrue(Physics2D.GetIgnoreCollision(collider, platform));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(collider, otherPlatform));

            executor.Dispose();
            Assert.IsFalse(Physics2D.GetIgnoreCollision(collider, platform));
        }

        /// <summary>Verifies that cancellation restores a temporary drop-through pair without clearing velocity.</summary>
        [UnityTest]
        public IEnumerator DropThrough_CancelRestoresCollisionWithoutClearingVelocity()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D platform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));
            executor.Tick(Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(1f, -2f);

            executor.Cancel();

            Assert.IsFalse(Physics2D.GetIgnoreCollision(collider, platform));
            Assert.AreEqual(new Vector2(1f, -2f), body.linearVelocity);
        }

        /// <summary>Verifies that clearing the selected platform restores collision before the step lands.</summary>
        [UnityTest]
        public IEnumerator DropThrough_ClearingPlatformRestoresCollision()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D platform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));
            executor.Tick(Time.fixedDeltaTime);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(collider, platform));

            body.position = new Vector2(0f, -1.2f);
            Physics2D.SyncTransforms();
            executor.Tick(Time.fixedDeltaTime);

            Assert.IsFalse(Physics2D.GetIgnoreCollision(collider, platform));
        }

        /// <summary>Verifies drop-through preserves a collider pair that another owner had already ignored.</summary>
        [UnityTest]
        public IEnumerator DropThrough_RestoresPreviouslyIgnoredPairState()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            Collider2D platform = CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.IgnoreCollision(collider, platform, true);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));

            executor.Tick(Time.fixedDeltaTime);
            executor.Cancel();

            Assert.IsTrue(Physics2D.GetIgnoreCollision(collider, platform));
            Physics2D.IgnoreCollision(collider, platform, false);
        }

        /// <summary>Verifies drop-through submits downward movement even when the body has no gravity.</summary>
        [UnityTest]
        public IEnumerator DropThrough_SubmitsDownwardVelocityFromTick()
        {
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(Vector2.zero);
            CreateFloor(new Vector2(0f, -0.55f), new Vector2(2f, 0.1f), NavigationPhysicsTestLayers.PlatformLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), 2f, 1f);
            executor.BeginDropThrough(new Vector2(0f, -0.5f), new Vector2(0f, -2f));

            Assert.That(executor.Tick(Time.fixedDeltaTime).Status, Is.EqualTo(ExecutionStatus.Running));

            Assert.Less(body.linearVelocityY, 0f);
        }

        private (Rigidbody2D body, BoxCollider2D collider) CreateBody(Vector2 position)
        {
            bodyObject = new GameObject("ground-traversal-body");
            bodyObject.transform.position = position;
            Rigidbody2D body = bodyObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = bodyObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return (body, collider);
        }

        /// <summary>Creates the package-local terrain configuration without a scene facade.</summary>
        private static ContactFilter2D CreateTerrainFilter()
            => NavigationPhysicsTestLayers.CreateTerrainFilter();

        private Collider2D CreateFloor(Vector2 position, Vector2 size, int layer)
        {
            floorObject = new GameObject("ground-traversal-floor") { layer = layer };
            floorObject.transform.position = position;
            BoxCollider2D collider = floorObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            ConfigureOneWay(floorObject, collider, layer);
            return collider;
        }

        private Collider2D CreateAdditionalPlatform(Vector2 position)
        {
            GameObject platform = new("ground-traversal-other-platform") { layer = NavigationPhysicsTestLayers.PlatformLayer };
            platform.transform.SetParent(floorObject.transform);
            platform.transform.position = position;
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2f, 0.1f);
            ConfigureOneWay(platform, collider, NavigationPhysicsTestLayers.PlatformLayer);
            return collider;
        }

        /// <summary>Creates an additional support under the fixture root so teardown remains owned by the test.</summary>
        private Collider2D CreateChildFloor(Vector2 position, Vector2 size, int layer)
        {
            GameObject support = new("ground-traversal-lower-support") { layer = layer };
            support.transform.SetParent(floorObject.transform);
            support.transform.position = position;
            BoxCollider2D collider = support.AddComponent<BoxCollider2D>();
            collider.size = size;
            ConfigureOneWay(support, collider, layer);
            return collider;
        }

        /// <summary>Configures a platform fixture with the real one-way effector contract.</summary>
        private static void ConfigureOneWay(GameObject gameObject, Collider2D collider, int layer)
        {
            if (layer != NavigationPhysicsTestLayers.PlatformLayer) return;
            collider.usedByEffector = true;
            PlatformEffector2D effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 90f;
            effector.rotationalOffset = 0f;
        }

        /// <summary>Gets the nearest physical support surface below the test body.</summary>
        private static float GetSupportSurfaceY(Collider2D collider)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = NavigationPhysicsTestLayers.TerrainMask,
                useTriggers = false,
            };
            RaycastHit2D[] hits = new RaycastHit2D[8];
            int count = collider.Cast(Vector2.down, filter, hits, 0.08f);
            float nearestDistance = float.PositiveInfinity;
            float surfaceY = float.NaN;
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                if (!hit.collider || hit.collider == collider || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                surfaceY = hit.point.y;
            }

            Assert.That(float.IsNaN(surfaceY), Is.False, "No physical support surface was found below the landed body.");
            return surfaceY;
        }
    }
}
