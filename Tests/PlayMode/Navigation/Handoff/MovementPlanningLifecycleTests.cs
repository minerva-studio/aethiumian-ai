using System.Collections;
using System.Collections.Generic;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies Movement's handoff of planning outcomes without prescribing planner or executor internals.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed partial class NavigationHandoffContractTests : MovementNodePackageFixture
    {
        private const float ArrivalErrorBound = 0.2f;
        private const int PlanningFrameLimit = 600;

        [TearDown]
        public void ResetControlledPlanner()
        {
            ControlledWalk.ResetTestState();
        }

        [UnityTest]
        public IEnumerator MovingTargetKeepsPendingPlanUntilItCompletes()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequest();

            ControlledWalk.Request first = ControlledWalk.Requests[0];
            for (int frame = 0; frame < 30; frame++)
            {
                target.transform.position += Vector3.right * (10f * Time.fixedDeltaTime);
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }

            // Position changes do not invalidate the owned Smart operation. The timed
            // fallback is still submitted once, but target motion must not duplicate it.
            Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(2));
            Assert.That(first.Operation.IsCompleted, Is.False);
            Assert.That(first.Operation.IsCancelled, Is.False);
            Assert.That(ControlledWalk.Requests[1].Extent, Is.EqualTo(NavigationPlanningExtent.NextAction));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator TraceTargetSideChangeCancelsStaleIntentBeforeReceiptSelection()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(1);

            ControlledWalk.Request first = ControlledWalk.Requests[0];
            ControlledWalk.Complete(first, CreateGroundRoute(first, new Vector2(35.5f, 1f), false));
            yield return WaitForRequestCount(2);
            ControlledWalk.Request staleContinuation = ControlledWalk.Requests[1];

            target.transform.position = new Vector2(20f, 1f);
            Physics2D.SyncTransforms();
            yield return WaitForRequestCount(3);

            Assert.That(staleContinuation.Operation.IsCancelled, Is.True, DescribeHarness(harness));
            Assert.That(ControlledWalk.Requests[2].Goal.Center.x, Is.EqualTo(20f).Within(0.001f), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));

            // The retained historical route must not keep invalidating the fresh intent.
            for (int frame = 0; frame < 5; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests.Count, Is.LessThanOrEqualTo(4), DescribeRequests());
            Assert.That(ControlledWalk.Requests[2].Operation.IsCancelled, Is.False, DescribeRequests());
        }

        [UnityTest]
        public IEnumerator ExactNoPathCompletesAsFailure()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequest();
            ControlledWalk.Complete(ControlledWalk.Requests[0], null);
            yield return WaitForTerminal(harness, PlanningFrameLimit);

            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator StaleNoPathStartsFreshRequest()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequest();
            target.transform.position = new Vector2(62.5f, 1f);
            Physics2D.SyncTransforms();
            ControlledWalk.Complete(ControlledWalk.Requests[0], null);
            for (int frame = 0; ControlledWalk.Requests.Count < 2 && frame < PlanningFrameLimit; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(ControlledWalk.Requests.Count, Is.GreaterThanOrEqualTo(2), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator CancelledRequestRecoversWithFreshRequest()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequest();
            ControlledWalk.Request first = ControlledWalk.Requests[0];
            ControlledWalk.Cancel(first);
            for (int frame = 0; ControlledWalk.Requests.Count < 2 && frame < PlanningFrameLimit; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(first.Operation.IsCancelled, Is.True);
            Assert.That(ControlledWalk.Requests.Count, Is.GreaterThanOrEqualTo(2), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator StopAndRestartIgnoresLatePreparedResult()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequest();

            ControlledWalk.Request first = ControlledWalk.Requests[0];
            NavigationRoute lateRoute = CreateGroundRoute(first, new Vector2(36.5f, 1f), completesGoal: false);
            Assert.That(first.Operation.TryPrepareCompletion(NavigationPlanResult.ResultProduced(lateRoute), out bool wasCancelled), Is.True);
            Assert.That(wasCancelled, Is.False);
            BehaviourTree tree = harness.AI.BehaviourTree;
            ControlledWalk movement = (ControlledWalk)tree.Head;
            Assert.That(tree.End(), Is.True, DescribeHarness(harness));
            Assert.That(tree.StartFromNode(movement), Is.True, DescribeHarness(harness));
            for (int frame = 0; ControlledWalk.Requests.Count < 2 && frame < PlanningFrameLimit; frame++)
                yield return new WaitForFixedUpdate();

            first.Operation.PublishPreparedCompletion();
            yield return new WaitForFixedUpdate();
            Assert.That(tree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(tree.IsFaulted, Is.False, DescribeHarness(harness));
            Assert.That(ControlledWalk.Requests[1].Operation.IsCompleted, Is.False, "The restarted movement must still own its latest pending planning result after the late publication.");
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
            ControlledWalk.Complete(ControlledWalk.Requests[1], CreateGroundRoute(ControlledWalk.Requests[1]));
            yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests[1].Operation.IsCompleted, Is.True);
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SmartWaitsFourFixedTicksThenRequestsOneNextActionFallback()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(1);

            ControlledWalk.Request smart = ControlledWalk.Requests[0];
            Assert.That(smart.Extent, Is.EqualTo(NavigationPlanningExtent.Route));
            for (int tick = 0; tick < 3; tick++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(1), DescribeHarness(harness));
            }

            yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(2), DescribeHarness(harness));
            Assert.That(ControlledWalk.Requests[1].Extent, Is.EqualTo(NavigationPlanningExtent.NextAction));
            Assert.That(smart.Operation.IsCompleted, Is.False);
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimpleFallbackNoResultKeepsOriginalSmartRequestAlive()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(2);

            ControlledWalk.Request smart = ControlledWalk.Requests[0];
            ControlledWalk.Request fallback = ControlledWalk.Requests[1];
            ControlledWalk.Complete(fallback, null);
            yield return new WaitForFixedUpdate();

            Assert.That(fallback.Operation.IsCompleted, Is.True);
            Assert.That(fallback.Operation.IsCancelled, Is.False);
            Assert.That(smart.Operation.IsCompleted, Is.False, "A missing local action must not turn the still-running Smart request into NoPath.");
            Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(2), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SmartResultWinsWhenPublishedWithFallback()
        {
            using MapNavigationRuntime runtime = CreateRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(2);

            ControlledWalk.Request smart = ControlledWalk.Requests[0];
            ControlledWalk.Request fallback = ControlledWalk.Requests[1];
            NavigationRoute smartRoute = CreateGroundRoute(smart);
            ControlledWalk movement = (ControlledWalk)harness.AI.BehaviourTree.Head;
            ControlledWalk.Complete(smart, smartRoute);
            ControlledWalk.Complete(fallback, CreateGroundRoute(fallback, new Vector2(32.5f, 1f), false));
            yield return new WaitForFixedUpdate();

            Assert.That(smart.Operation.IsCancelled, Is.False);
            Assert.That(fallback.Operation.IsCompleted, Is.True);
            Assert.That(movement.Route, Is.Not.Null, DescribeHarness(harness));
            Assert.That(movement.Route.Segments[0].End.x, Is.EqualTo(smart.Goal.Center.x).Within(0.25f), "A fallback result published in the same fixed tick must not replace Smart.");
            Assert.That(ControlledWalk.Requests.Count, Is.GreaterThanOrEqualTo(2), DescribeHarness(harness));
            if (ControlledWalk.Requests.Count >= 3)
            {
                Assert.That(ControlledWalk.Requests[2].Extent, Is.EqualTo(NavigationPlanningExtent.Route));
                Assert.That(ControlledWalk.Requests[2].Purpose, Is.EqualTo(NavigationPlanningPurpose.EndpointContinuation));
            }
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.GreaterThan(0), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SmartResultReplacesCommittedFallbackAfterPhysicalReconnect()
        {
            using MapNavigationRuntime runtime = CreateRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(2);

            ControlledWalk.Request smart = ControlledWalk.Requests[0];
            ControlledWalk.Request fallback = ControlledWalk.Requests[1];
            Vector2 fallbackEndpoint = new(32.5f, 1f);
            ControlledWalk.Complete(fallback, CreateGroundRoute(fallback, fallbackEndpoint, false));
            yield return new WaitForFixedUpdate();

            ControlledWalk movement = (ControlledWalk)harness.AI.BehaviourTree.Head;
            Assert.That(smart.Operation.IsCancelled, Is.False);
            Assert.That(fallback.Operation.IsCancelled, Is.False);
            Assert.That(movement.ActiveSegment, Is.Not.Null, DescribeHarness(harness));
            Assert.That(movement.Route.Segments[0].End.x, Is.EqualTo(fallbackEndpoint.x).Within(0.25f), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.GreaterThan(0), DescribeHarness(harness));

            // Smart is allowed to interrupt a reversible Simple fallback before that action
            // reaches its endpoint.
            ControlledWalk.Complete(smart, CreateGroundRoute(smart, new Vector2(40.5f, 1f), false));
            yield return new WaitForFixedUpdate();

            Assert.That(movement.Route, Is.Not.Null, DescribeHarness(harness));
            Assert.That(movement.Route.Segments[0].End.x, Is.EqualTo(40.5f).Within(0.25f), "A ready Smart result must replace a committed fallback once the route reconnects to the real body.");
            Assert.That(harness.Source.WalkCount, Is.GreaterThan(0), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator PausedMovementDoesNotAccumulateSmartFallbackTicks()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target), canMove: false);
            yield return WaitForTreeCreated(harness);

            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests, Is.Empty, DescribeHarness(harness));

            harness.Source.CanMove = true;
            yield return WaitForRequestCount(1);
            for (int tick = 0; tick < 3; tick++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(1), DescribeHarness(harness));
            }
        }

        [UnityTest]
        public IEnumerator SmartFallbackBackoffEscalatesToEightAndSixteenTicks()
        {
            using MapNavigationRuntime runtime = CreateRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(2);

            int[] thresholds = { 8, 16 };
            float[] endpoints = { 32.5f, 34.5f };
            for (int index = 0; index < thresholds.Length; index++)
            {
                ControlledWalk.Request fallback = ControlledWalk.Requests[1 + index * 2];
                ControlledWalk.Complete(fallback, CreateGroundRoute(
                    fallback, new Vector2(endpoints[index], 1f), false));
                // A committed fallback does not cancel Smart. Resolve that Smart request as a
                // terminal miss so this test can exercise endpoint continuation and backoff.
                yield return new WaitForFixedUpdate();
                ControlledWalk.Request smart = ControlledWalk.Requests[index * 2];
                ControlledWalk.Complete(smart, null);
                yield return WaitForRequestCount(3 + index * 2);

                ControlledWalk movement = (ControlledWalk)harness.AI.BehaviourTree.Head;
                int frame = 0;
                while (movement.ActiveSegment != null && frame++ < 300)
                    yield return new WaitForFixedUpdate();
                Assert.That(movement.ActiveSegment, Is.Null, DescribeHarness(harness));

                int requestCountBeforeThreshold = ControlledWalk.Requests.Count;
                for (int tick = 0; tick < thresholds[index] - 1; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(requestCountBeforeThreshold),
                        DescribeHarness(harness));
                }

                yield return new WaitForFixedUpdate();
                Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(requestCountBeforeThreshold + 1),
                    DescribeHarness(harness));
                Assert.That(ControlledWalk.Requests[^1].Extent, Is.EqualTo(NavigationPlanningExtent.NextAction));
            }
        }

        [UnityTest]
        public IEnumerator EndingMovementCancelsSmartAndFallbackRequests()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            yield return WaitForRequestCount(2);

            ControlledWalk.Request smart = ControlledWalk.Requests[0];
            ControlledWalk.Request fallback = ControlledWalk.Requests[1];
            Assert.That(harness.AI.BehaviourTree.End(), Is.True, DescribeHarness(harness));
            Assert.That(smart.Operation.IsCancelled, Is.True);
            Assert.That(fallback.Operation.IsCancelled, Is.True);
        }

        private static MapNavigationRuntime CreateRuntime(float groundY = 0f)
        {
            MapNavigationRuntime runtime = new(
                8,
                4096,
                4096,
                new NavigationPhysicsLayers(
                    NavigationPhysicsTestLayers.GeometryMask,
                    NavigationPhysicsTestLayers.PlatformMask));
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.Ground(groundY));
            return runtime;
        }

        private static ControlledWalk CreateControlledWalkTrace(GameObject target)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.Trace,
                goal = MovementGoal.Default,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
            };

        /// <summary>Builds a contract-valid route for a captured controlled request.</summary>
        private static NavigationRoute CreateGroundRoute(
            ControlledWalk.Request request,
            Vector2? endpoint = null,
            bool completesGoal = true)
        {
            Vector2 resolvedGoal = endpoint ?? new Vector2(request.Goal.Center.x, 1f);
            Vector2 logicalStart = new(request.Start.x, 1f);
            Vector2 bodyCenter = resolvedGoal + Vector2.up * (BodyHeight * 0.5f);
            bool reachesGoal = request.Goal.IsComplete(bodyCenter, new Vector2(BodyWidth, BodyHeight));
            if (completesGoal)
                Assert.That(reachesGoal, Is.True,
                    $"Fixture endpoint {resolvedGoal} does not satisfy the captured goal {request.Goal}.");
            return NavigationRoute.Create(logicalStart, request.Goal, resolvedGoal,
                new[] { new GroundRouteSegment(logicalStart, resolvedGoal) },
                reachesGoal);
        }

        private static IEnumerator WaitForRequest()
        {
            for (int frame = 0; ControlledWalk.Requests.Count == 0 && frame < PlanningFrameLimit; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(1));
        }

        private sealed class ControlledWalk : Walk
        {
            internal sealed class Request
            {
                internal NavigationPlanningOperation Operation { get; }
                internal Vector2 Start { get; }
                internal NavigationGoalRegion Goal { get; }
                internal NavigationPlanningExtent Extent { get; }
                internal NavigationPlanningPurpose Purpose { get; }

                internal Request(NavigationPlanningOperation operation, Vector2 start, NavigationGoalRegion goal,
                    NavigationPlanningExtent extent, NavigationPlanningPurpose purpose)
                {
                    Operation = operation;
                    Start = start;
                    Goal = goal;
                    Extent = extent;
                    Purpose = purpose;
                }
            }

            internal static readonly List<Request> Requests = new();

            internal static void ResetTestState() => Requests.Clear();

            internal static void Complete(Request request, NavigationRoute route)
                => Assert.That(request.Operation.TryComplete(route == null
                    ? NavigationPlanResult.NoResult
                    : NavigationPlanResult.ResultProduced(route)), Is.True);

            internal static void Cancel(Request request)
                => Assert.That(request.Operation.TryFinalizeCancellation(), Is.True);

            protected override bool TryRequestRoute(
                Vector2 start,
                NavigationGoalRegion goal,
                NavigationPlanningExtent extent,
                NavigationPlanningPurpose purpose,
                System.Threading.CancellationToken cancellationToken,
                out NavigationPlanningOperation operation)
            {
                operation = new NavigationPlanningOperation();
                operation.RegisterCancellation(cancellationToken);
                Requests.Add(new Request(operation, start, goal, extent, purpose));
                return true;
            }
        }

        private static IEnumerator WaitForRequestCount(int count)
        {
            for (int frame = 0; ControlledWalk.Requests.Count < count && frame < PlanningFrameLimit; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(ControlledWalk.Requests.Count, Is.GreaterThanOrEqualTo(count),
                DescribeRequests());
        }

        private static string DescribeRequests()
        {
            if (ControlledWalk.Requests.Count == 0) return "No controlled planning requests were recorded.";
            List<string> descriptions = new(ControlledWalk.Requests.Count);
            for (int index = 0; index < ControlledWalk.Requests.Count; index++)
            {
                ControlledWalk.Request request = ControlledWalk.Requests[index];
                descriptions.Add($"#{index}: {request.Extent}/{request.Purpose} Start={request.Start} "
                    + $"Completed={request.Operation.IsCompleted} Cancelled={request.Operation.IsCancelled} "
                    + $"Termination={request.Operation.PlanResult.Termination}");
            }
            return string.Join("; ", descriptions);
        }
    }
}
