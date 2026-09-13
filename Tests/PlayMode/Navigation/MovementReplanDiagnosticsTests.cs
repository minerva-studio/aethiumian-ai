using System.Collections;
using Aethiumian.AI;
using Aethiumian.AI.Diagnostics;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Navigation.Diagnostics;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>Verifies package-owned replan diagnostics with a local runtime and physics fixture.</summary>
    public sealed class MovementReplanDiagnosticsTests : MovementNodePackageFixture
    {
        private const int PlanningFrameLimit = 600;

        [TearDown]
        public void ResetDiagnostics()
        {
            MovementReplanDiagnostics.Enabled = false;
            MovementReplanDiagnostics.Reset();
            AIPerformanceDiagnostics.Enabled = false;
            AIPerformanceDiagnostics.Reset();
        }

        [UnityTest]
        public IEnumerator TargetMicroMotionDoesNotReplan()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 5f));
            MovementHarness harness = CreateHarness(MovementStart, CreateSmartWalkTrace(target));
            yield return WaitForTreeCreated(harness);
            MovementReplanDiagnostics.Enabled = true;
            MovementReplanDiagnostics.Reset();
            yield return new WaitForFixedUpdate();
            MovementReplanDiagnostics.Reset();

            for (int frame = 0; frame < 30; frame++)
            {
                target.transform.position += Vector3.right * 0.02f;
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }

            MovementReplanDiagnostics.Snapshot snapshot = MovementReplanDiagnostics.Capture();
            Assert.That(snapshot.GoalChanges, Is.EqualTo(0));
            Assert.That(snapshot.GoalPendingCancellations, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OwnMovementDoesNotChangeFixedGoal()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            MovementHarness harness = CreateHarness(MovementStart, CreateSmartWalk(MovementStart + Vector2.right * 26f));
            yield return WaitForTreeCreated(harness);
            MovementReplanDiagnostics.Enabled = true;
            MovementReplanDiagnostics.Reset();
            yield return new WaitForFixedUpdate();
            MovementReplanDiagnostics.Reset();

            Vector2 initialPosition = harness.Body.position;
            int frame = 0;
            while (Vector2.Distance(harness.Body.position, initialPosition) <= 0.5f
                && frame++ < PlanningFrameLimit)
                yield return new WaitForFixedUpdate();

            MovementReplanDiagnostics.Snapshot snapshot = MovementReplanDiagnostics.Capture();
            Assert.That(Vector2.Distance(harness.Body.position, initialPosition), Is.GreaterThan(0.5f));
            Assert.That(snapshot.AnchorMoves, Is.GreaterThan(0));
            Assert.That(snapshot.GoalChanges, Is.Zero);
        }

        private static MapNavigationRuntime CreateRuntime()
        {
            MapNavigationRuntime runtime = new(8, 4096, 4096);
            runtime.PublishWorld(CreateGroundWorld());
            return runtime;
        }

        private static Walk CreateSmartWalk(Vector2 goal)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(goal),
                reachDistance = (VariableField<float>)0.2f,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
                setFinalPosition = (VariableField<bool>)false,
            };

        private static Walk CreateSmartWalkTrace(GameObject target)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.Trace,
                goal = MovementGoal.Default,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)0.2f,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
            };
    }
}
