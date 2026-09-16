using System;
using System.Collections;
using System.Collections.Generic;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies movement goal completion against package-owned runtime contracts.</summary>
    public sealed class MovementGoalContractTests : MovementNodePackageFixture
    {
        private const float ArrivalErrorBound = 0.2f;
        private const int GoalTickLimit = 240;
        private const float DirectionNoise = 0.25f;

        [UnityTest]
        public IEnumerator FlyCurrentPointCompletesAndClearsVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Vector2 center = new(40.5f, 4f);
            GameObject target = CreateTraceTarget(center);
            MovementHarness harness = CreateHarness(center - Vector2.up * (BodyHeight * 0.5f), CreateFlyTrace(target));
            harness.Body.gravityScale = 0f;
            harness.Body.linearVelocity = new Vector2(4f, -2f);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity, Is.EqualTo(Vector2.zero).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator FlyHighSpeedCrossingCompletesAndClearsVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Vector2 center = new(40.5f, 4f);
            GameObject target = CreateTraceTarget(center);
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), CreateFlyTrace(target));
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            yield return new WaitForFixedUpdate();

            harness.Body.position = new Vector2(center.x - 2f, center.y);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            harness.Body.position = new Vector2(center.x + 2f, center.y);
            harness.Body.linearVelocity = new Vector2(300f, 40f);
            Physics2D.SyncTransforms();
            yield return WaitForTerminal(harness, 4);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity, Is.EqualTo(Vector2.zero).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator WalkCurrentPointCompletesWithoutMovement()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            MovementHarness harness = CreateHarness(MovementStart, CreateFixedWalk(MovementStart));
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.x, Is.EqualTo(0f).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimpleNoPathFailsAndPreservesVerticalVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Walk node = CreateFixedWalk(new Vector2(MovementStart.x, 14f));
            node.path = Movement.PathMode.Simple;
            MovementHarness harness = CreateHarness(new Vector2(MovementStart.x, 1f), node);
            harness.Body.gravityScale = 0f;
            harness.Body.linearVelocity = new Vector2(3f, 2f);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.x, Is.EqualTo(0f).Within(0.001f), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.y, Is.GreaterThan(0.05f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimplePlateauDoesNotOscillate()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 5.9f));
            target.GetComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            Walk node = CreateTraceWalk(target);
            node.path = Movement.PathMode.Simple;
            MovementHarness harness = CreateHarness(MovementStart, node);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);

            List<float> directions = new();
            for (int tick = 0; tick < GoalTickLimit && harness.AI.BehaviourTree.IsRunning; tick++)
            {
                yield return new WaitForFixedUpdate();
                float velocityX = harness.Body.linearVelocity.x;
                if (Mathf.Abs(velocityX) >= DirectionNoise) directions.Add(Mathf.Sign(velocityX));
            }

            int reversals = 0;
            for (int index = 1; index < directions.Count; index++)
                if (directions[index] != directions[index - 1]) reversals++;
            Assert.That(reversals, Is.EqualTo(0), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator DirectWanderArrivalUsesGroundWalkSurfaceGap()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            MovementHarness harness = CreateHarness(MovementStart, CreateDirectWander(new Vector2(31f, 1f)));
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimpleRetreatCompletesImmediatelyWhenAlreadyBeyondReachDistance()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(36.5f, 1.9f));
            MovementHarness harness = CreateHarness(MovementStart + Vector2.up, CreateSimpleRetreat(target, 3f));
            harness.Body.gravityScale = 0f;
            Vector2 initialBodyPosition = harness.Body.position;
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, 4);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Body.position.x, Is.EqualTo(initialBodyPosition.x).Within(0.0001f));
            Assert.That(harness.Body.position.y, Is.EqualTo(initialBodyPosition.y).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator SimpleRetreatNoProgressCompletesAsFailure()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(33f, 1.9f));
            Fly node = CreateSimpleRetreat(target, 3f);
            node.speed = (VariableField<float>)0f;
            node.maxIdleDuration = (VariableField<float>)0.1f;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
        }

        private static MapNavigationRuntime CreateRuntime()
        {
            MapNavigationRuntime runtime = new(8, 4096, 4096);
            Rect bounds = new(-100f, -100f, 200f, 200f);
            runtime.PublishWorld(NavigationWorldSnapshot.Create(
                bounds,
                Array.Empty<NavigationShapeData>(),
                new[] { new NavigationRegionData(bounds, 0) }));
            return runtime;
        }


        private static Fly CreateFlyTrace(GameObject target, MovementGoal goal = MovementGoal.Default)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Trace,
                goal = goal,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                speed = (VariableField<float>)10f,
                speedModifier = (VariableField<float>)1f,
            };

        private static Walk CreateFixedWalk(Vector2 destination)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(destination),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
                setFinalPosition = (VariableField<bool>)false,
            };

        private static Walk CreateTraceWalk(GameObject target)
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

        private static Walk CreateDirectWander(Vector2 center, float reachDistance = ArrivalErrorBound)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Wander,
                wanderMode = Movement.WanderMode.AbsoluteCentered,
                centerSpace = Space.World,
                centerOfWander = new VariableField(center),
                wanderDistance = (VariableField<float>)0f,
                reachDistance = (VariableField<float>)reachDistance,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                setFinalPosition = (VariableField<bool>)false,
            };

        private static Fly CreateSimpleRetreat(GameObject target, float reachDistance)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Retreat,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)reachDistance,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
            };
    }

}
