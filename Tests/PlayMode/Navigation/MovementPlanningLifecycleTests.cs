using System;
using System.Collections;
using System.Collections.Generic;
using Aethiumian.AI;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>Verifies package movement request ownership without a project Map or room fixture.</summary>
    public sealed class MovementPlanningLifecycleTests : MovementNodePackageFixture
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

            // Position changes are coalesced into the next goal sample; they do not
            // invalidate an already-owned planning operation.
            Assert.That(ControlledWalk.Requests.Count, Is.EqualTo(1));
            Assert.That(first.Operation.IsCompleted, Is.False);
            Assert.That(first.Operation.IsCancelled, Is.False);
            Assert.That(first.Goal.Center.x, Is.EqualTo(56.5f).Within(0.001f));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
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
            NavigationRoute lateRoute = CreateGroundRoute(first, new Vector2(36.5f, 1f));
            Assert.That(first.Operation.TryPrepareCompletion(
                new NavigationPlanResult(lateRoute, NavigationPlanTermination.ResultProduced), out bool wasCancelled), Is.True);
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

        private static MapNavigationRuntime CreateRuntime()
        {
            MapNavigationRuntime runtime = new(8, 4096, 4096);
            runtime.PublishWorld(CreateGroundWorld());
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

        private static NavigationRoute CreateGroundRoute(ControlledWalk.Request request, Vector2? endpoint = null)
        {
            Vector2 resolvedGoal = endpoint ?? request.Goal.Center;
            return NavigationRoute.Create(request.Start, request.Goal, resolvedGoal,
                new[] { new GroundRouteSegment(request.Start, resolvedGoal) }, true);
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

                internal Request(NavigationPlanningOperation operation, Vector2 start, NavigationGoalRegion goal)
                {
                    Operation = operation;
                    Start = start;
                    Goal = goal;
                }
            }

            internal static readonly List<Request> Requests = new();

            internal static void ResetTestState() => Requests.Clear();

            internal static void Complete(Request request, NavigationRoute route)
                => Assert.That(request.Operation.TryComplete(new NavigationPlanResult(
                    route, NavigationPlanTermination.ResultProduced)), Is.True);

            internal static void Cancel(Request request)
                => Assert.That(request.Operation.TryFinalizeCancellation(), Is.True);

            protected override bool TryRequestRoute(
                Vector2 start,
                NavigationGoalRegion goal,
                NavigationPlanningPurpose purpose,
                System.Threading.CancellationToken cancellationToken,
                out NavigationPlanningOperation operation)
            {
                operation = new NavigationPlanningOperation();
                operation.RegisterCancellation(cancellationToken);
                Requests.Add(new Request(operation, start, goal));
                return true;
            }
        }
    }
}
