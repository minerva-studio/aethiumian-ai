using System;
using System.Collections;
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    /// <summary>Exercises admission nodes through the actual child call stack and interruption paths.</summary>
    public sealed class TimerChildExecutionTests
    {
        /// <summary>Verifies Cooldown Consumes Only Successful Child And Reads Latest Duration.</summary>
        [UnityTest]
        public IEnumerator CooldownConsumesOnlySuccessfulChildAndReadsLatestDuration()
        {
            VariableData timer = TimerData();
            VariableData duration = new("Duration", VariableType.Float);
            duration.SetDefaultValue(2f);
            Cooldown head = TreeTestFixture.CreateNode<Cooldown>("Cooldown");
            PendingAction child = TreeTestFixture.CreateNode<PendingAction>("Child");
            head.timer.SetReference(timer);
            head.duration.SetReference(duration);
            head.node = child.ToReference();
            using TreeTestFixture fixture = TreeTestFixture.Create(head, new[] { timer, duration }, child);
            yield return fixture.WaitUntilReady();
            PendingAction runtime = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => runtime.Starts == 1);
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.Zero);
            runtime.Finish(false);
            yield return fixture.WaitUntil(() => !fixture.Tree.IsRunning);
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.Zero);
            fixture.Start();
            yield return fixture.WaitUntil(() => runtime.Starts == 2);
            fixture.Tree.End();
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.Zero);
            fixture.Start();
            yield return fixture.WaitUntil(() => runtime.Starts == 3);
            fixture.Tree.Variables[duration.UUID].SetValue(6f);
            runtime.Finish(true);
            yield return fixture.WaitUntil(() => !fixture.Tree.IsRunning);
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.GreaterThan(2f).And.LessThanOrEqualTo(6f));
            fixture.Start();
            yield return fixture.WaitUntil(() => !fixture.Tree.IsRunning);
            Assert.That(runtime.Starts, Is.EqualTo(3));
        }

        /// <summary>Verifies Throttle Consumes Before Child And Retains Across Interruption.</summary>
        [UnityTest]
        public IEnumerator ThrottleConsumesBeforeChildAndRetainsAcrossInterruption()
        {
            VariableData timer = TimerData();
            Throttle head = TreeTestFixture.CreateNode<Throttle>("Throttle");
            PendingAction child = TreeTestFixture.CreateNode<PendingAction>("Child");
            head.timer.SetReference(timer);
            head.duration = 10f;
            head.node = child.ToReference();
            using TreeTestFixture fixture = TreeTestFixture.Create(head, new[] { timer }, child);
            yield return fixture.WaitUntilReady();
            PendingAction runtime = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => runtime.Starts == 1);
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.GreaterThan(0f));
            fixture.Tree.End();
            fixture.Tree.Restart();
            yield return fixture.WaitUntil(() => !fixture.Tree.IsRunning);
            Assert.That(runtime.Starts, Is.EqualTo(1));
            Assert.That(fixture.Tree.Variables[timer.UUID].FloatValue, Is.GreaterThan(0f));
        }

        /// <summary>Creates the per-tree timer definition used by each fixture.</summary>
        private static VariableData TimerData()
        {
            VariableData data = new("Timer", VariableType.Float);
            data.Flags |= VariableFlag.Timer;
            return data;
        }

        [Serializable]
        private sealed class PendingAction : Aethiumian.AI.Nodes.Action
        {
            public int Starts;
            /// <summary>Records admission and waits for an explicit test completion signal.</summary>
            public override void Start() => Starts++;
            /// <summary>Completes the actual action through its normal result channel.</summary>
            public void Finish(bool result) => End(result);
        }
    }
}
