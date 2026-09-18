using System;
using System.Collections;
using System.Threading;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    public sealed class FlyHeightMovementTests : MovementNodePackageFixture
    {
        /// <summary>Direct steering and Smart requests share support-relative bounds without resizing them.</summary>
        [UnityTest]
        public IEnumerator FlyHeight_TraceSharesAdjustedBoundsAcrossExecutionModes()
        {
            foreach (string path in new[] { "Simple", "Smart" })
            {
                using MapNavigationRuntime runtime = new(8, 4096, 4096);
                runtime.PublishWorld(CreateFlyHeightWorld(5f));
                using RuntimeContextScope context = new(runtime);
                GameObject target = CreateTraceTarget(new Vector2(34f, 15f));
                TreeNode template = CreateHeightQueryFly(path, target);
                MovementHarness harness = CreateHarness(MovementStart, template);
                harness.Body.gravityScale = 0f;
                harness.Body.linearDamping = 0f;
                yield return WaitForTreeCreated(harness);
                var fly = (HeightQueryFly)harness.AI.BehaviourTree.Head;
                yield return new WaitForFixedUpdate();
                AABB goal = fly.CaptureGoal().TargetBounds;
                Assert.That(goal.Center.y, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(goal.Size, Is.EqualTo((Vector2)target.GetComponent<Collider2D>().bounds.size));

                harness.Source.CanMove = true;
                for (int tick = 0; tick < RuntimeContractTickLimit && harness.Body.linearVelocity.sqrMagnitude < 0.01f; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.That(harness.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(0.01f), DescribeHarness(harness));
                if (path == "Smart")
                {
                    Assert.That(fly.PlannedGoal.HasValue, Is.True);
                    Assert.That(fly.PlannedGoal.Value.Center.y, Is.EqualTo(7f).Within(0.0001f));
                    Assert.That(fly.PlannedGoal.Value.Size, Is.EqualTo(goal.Size));
                }
                else
                {
                    Assert.That(fly.PlannedGoal.HasValue, Is.True);
                    Assert.That(fly.PlannedGoal.Value.Center.y, Is.EqualTo(7f).Within(0.0001f));
                    Assert.That(fly.PlannedGoal.Value.Size, Is.EqualTo(goal.Size));
                }
                harness.AI.End(false);
                Assert.That(runtime.IsDisposed, Is.False);
            }
        }

        /// <summary>Missing support leaves the target untouched, and the Trace switch does not alter fixed goals.</summary>
        [UnityTest]
        public IEnumerator FlyHeight_NoSupportAndUnrestrictedModesKeepTarget()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096);
            runtime.PublishWorld(CreateFlyHeightWorld(null));
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(34f, 15f));
            MovementHarness harness = CreateHarness(MovementStart, CreateHeightQueryFly("Simple", target));
            yield return WaitForTreeCreated(harness);
            var fly = (HeightQueryFly)harness.AI.BehaviourTree.Head;
            yield return new WaitForFixedUpdate();
            Assert.That(fly.CaptureGoal().TargetBounds.Center.y, Is.EqualTo(15f));
            harness.AI.End(false);

            using MapNavigationRuntime supported = new(8, 4096, 4096);
            supported.PublishWorld(CreateFlyHeightWorld(5f));
            using RuntimeContextScope supportedContext = new(supported);
            HeightQueryFly template = CreateHeightQueryFly("Simple", target);
            template.type = Movement.Behaviour.FixedDestination;
            template.destination = new VariableField(new Vector2(34f, 15f));
            MovementHarness fixedHarness = CreateHarness(MovementStart, template);
            yield return WaitForTreeCreated(fixedHarness);
            var fixedFly = (HeightQueryFly)fixedHarness.AI.BehaviourTree.Head;
            yield return new WaitForFixedUpdate();
            Assert.That(fixedFly.CaptureGoal().TargetBounds.Center.y, Is.EqualTo(15f));
            fixedHarness.AI.End(false);
        }

        /// <summary>World publication and movement permission precede the single Wander selection.</summary>
        [UnityTest]
        public IEnumerator FlyHeight_WanderWaitsForReadyAndPermissionBeforeSelecting()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096);
            using RuntimeContextScope context = new(runtime);
            TreeNode template = CreateHeightWander(new Vector2(45f, 12f));
            MovementHarness harness = CreateHarness(MovementStart, template);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            var fly = (HeightQueryFly)harness.AI.BehaviourTree.Head;
            Vector2 start = harness.Body.position;
            for (int tick = 0; tick < 3; tick++) yield return new WaitForFixedUpdate();
            Assert.That(fly.WanderSelections, Is.Zero);
            Assert.That(Vector2.Distance(harness.Body.position, start), Is.LessThanOrEqualTo(0.01f));
            Assert.That(harness.Body.linearVelocity, Is.EqualTo(Vector2.zero));

            // If Awake cached a fallback or selected early, this changed authored center would be lost.
            fly.centerOfWander = new VariableField(new Vector2(50f, 12f));
            harness.Source.CanMove = false;
            runtime.PublishWorld(CreateFlyHeightWorld(5f));
            yield return new WaitForFixedUpdate();
            Assert.That(fly.WanderSelections, Is.Zero);
            harness.Source.CanMove = true;
            for (int tick = 0; tick < RuntimeContractTickLimit && fly.WanderSelections == 0; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(fly.WanderSelections, Is.EqualTo(1));
            Assert.That(fly.SelectedWander, Is.EqualTo(new Vector2(50f, 7f)));
            harness.AI.End(false);
            Assert.That(runtime.IsDisposed, Is.False);
        }

        /// <summary>Cancellation and runtime disposal terminate a waiting Fly without generating a Wander target.</summary>
        [UnityTest]
        public IEnumerator FlyHeight_WaitingCancellationAndDisposalReleaseBorrowedState()
        {
            foreach (bool dispose in new[] { false, true })
            {
                using MapNavigationRuntime runtime = new(8, 4096, 4096);
                using RuntimeContextScope context = new(runtime);
                MovementHarness harness = CreateHarness(MovementStart, CreateHeightWander(new Vector2(50f, 12f)));
                harness.Body.gravityScale = 0f;
                yield return WaitForTreeCreated(harness);
                var fly = (HeightQueryFly)harness.AI.BehaviourTree.Head;
                harness.Source.CanMove = false;
                if (dispose)
                {
                    runtime.Dispose();
                    yield return new WaitForFixedUpdate();
                    Assert.That(fly.IsComplete, Is.True);
                    Assert.That(fly.NavigationRuntime, Is.Null);
                    Assert.That(fly.WanderSelections, Is.Zero);
                    yield return WaitForTerminal(harness, RuntimeContractTickLimit);
                }
                else
                {
                    harness.AI.End(false);
                    Assert.That(harness.AI.BehaviourTree.IsRunning, Is.False);
                    Assert.That(fly.NavigationRuntime, Is.Null);
                    Assert.That(fly.WanderSelections, Is.Zero);
                }
                Assert.That(runtime.IsDisposed, Is.EqualTo(dispose));
            }
        }

        /// <summary>Lowering a Wander point must not place the body inside its support.</summary>
        [UnityTest]
        public IEnumerator FlyHeight_WanderRechecksBodyClearanceAfterLowering()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096);
            runtime.PublishWorld(CreateFlyHeightWorld(5f, solid: true));
            using RuntimeContextScope context = new(runtime);
            HeightQueryFly template = CreateHeightWander(new Vector2(50f, 12f));
            template.maxHeight = (VariableField<float>)0.25f;
            LogAssert.Expect(LogType.Warning, "Cannot find valid wander location around. Is the entity outside the room?");
            MovementHarness harness = CreateHarness(MovementStart, template);
            yield return WaitForTreeCreated(harness);
            var fly = (HeightQueryFly)harness.AI.BehaviourTree.Head;
            yield return new WaitForFixedUpdate();
            Assert.That(fly.WanderSelections, Is.EqualTo(1));
            Assert.That(fly.SelectedWander.x, Is.Not.EqualTo(50),
                "The lowered center at y=5.25 intersects the solid support and must use the existing fallback.");
            harness.AI.End(false);
        }

        private static HeightQueryFly CreateHeightQueryFly(string path, GameObject target)
        {
            return new HeightQueryFly
            {
                uuid = UUID.NewUUID(),
                path = path == "Smart" ? Movement.PathMode.Smart : Movement.PathMode.Simple,
                type = Movement.Behaviour.Trace,
                tracing = new VariableField(target),
                neverAboveMaxHeight = true,
                maxHeight = (VariableField<float>)2f,
                reachDistance = (VariableField<float>)0.1f,
                speed = (VariableField<float>)5f,
                flexibility = (VariableField<float>)1f,
            };
        }

        private static HeightQueryFly CreateHeightWander(Vector2 center)
        {
            HeightQueryFly node = CreateHeightQueryFly("Simple", null);
            node.type = Movement.Behaviour.Wander;
            node.wanderMode = Movement.WanderMode.AbsoluteCentered;
            node.centerOfWander = new VariableField(center);
            node.centerSpace = Space.World;
            node.wanderDistance = (VariableField<float>)0f;
            return node;
        }

        private static NavigationWorldSnapshot CreateFlyHeightWorld(float? surfaceY, bool solid = false)
        {
            NavigationShapeData[] shapes = Array.Empty<NavigationShapeData>();
            if (surfaceY.HasValue)
            {
                float y = surfaceY.Value;
                Vector2[] vertices = solid
                    ? new[] { new Vector2(20f, y - 1f), new Vector2(70f, y - 1f), new Vector2(70f, y), new Vector2(20f, y) }
                    : new[] { new Vector2(20f, y), new Vector2(70f, y) };
                shapes = new[] { new NavigationShapeData(1, 0,
                    solid ? NavigationShapeType.Polygon : NavigationShapeType.Edge, vertices, 0f,
                    solid ? NavigationSurfaceKind.Solid : NavigationSurfaceKind.OneWay, true, Vector2.up) };
            }
            AABB bounds = AABB.FromMinAndSize(0f, 0f, 80f, 40f);
            return NavigationWorldSnapshot.Create(bounds, shapes,
                new[] { new NavigationRegionData(bounds, 0) });
        }

        /// <summary>Observes existing protected boundaries without replacing goal selection or execution.</summary>
        [Serializable]
        public sealed class HeightQueryFly : Fly
        {
            public int WanderSelections { get; private set; }
            public Vector2 SelectedWander { get; private set; }
            public AABB? PlannedGoal { get; private set; }
            public NavigationGoalRequest CaptureGoal()
            {
                AABB target = type == Movement.Behaviour.FixedDestination
                    ? AABB.Point(destination.Vector2Value)
                    : tracing != null && tracing.GameObjectValue
                        ? AABB.FromBounds(tracing.GameObjectValue.GetComponent<Collider2D>().bounds)
                        : AABB.Point(Vector3.zero);
                return BuildGoal(target, NavigationBodyAabb);
            }

            protected override Vector2 GetWanderLocation(Vector2 center, AABB body)
            {
                WanderSelections++;
                return SelectedWander = base.GetWanderLocation(center, body);
            }

            protected override bool TryRequestRoute(
                AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent,
                NavigationPlanningPurpose purpose,
                CancellationToken cancellationToken, out NavigationPlanningOperation operation)
            {
                PlannedGoal = goal.TargetBounds;
                return base.TryRequestRoute(body, goal, extent, purpose, cancellationToken, out operation);
            }
        }
    }
}
