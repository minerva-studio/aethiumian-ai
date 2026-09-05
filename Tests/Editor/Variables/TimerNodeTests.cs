using System;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Checks timer consumption boundaries without depending on elapsed wall time.</summary>
    public sealed class TimerNodeTests
    {
        /// <summary>Creates an initialized timer with a stable authored identity.</summary>
        private static TimerVariable CreateTimer()
        {
            VariableData data = new("Timer", VariableType.Float);
            data.Flags |= VariableFlag.Timer;
            return new TimerVariable(new BehaviourTreeTimer(default), data.UUID, data.name);
        }

        /// <summary>Verifies Cooldown Without Child Consumes Once And Rejects While Cooling.</summary>
        [Test]
        public void CooldownWithoutChildConsumesOnceAndRejectsWhileCooling()
        {
            TimerVariable timer = CreateTimer();
            Cooldown node = new() { duration = 10f };
            node.timer.SetRuntimeReference(timer);
            node.Initialize();
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(timer.Remaining, Is.GreaterThan(0f));
            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            node.Initialize();
            Assert.That(timer.Remaining, Is.GreaterThan(0f));
        }

        /// <summary>Verifies Cooldown Reads Duration Only On Success.</summary>
        [Test]
        public void CooldownReadsDurationOnlyOnSuccess()
        {
            TimerVariable timer = CreateTimer();
            Cooldown node = new() { duration = float.NaN };
            node.timer.SetRuntimeReference(timer);
            Assert.That(node.ReceiveReturnFromChild(false), Is.EqualTo(State.Failed));
            Assert.That(timer.Remaining, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => node.ReceiveReturnFromChild(true));
            node.duration = 7f;
            Assert.That(node.ReceiveReturnFromChild(true), Is.EqualTo(State.Success));
            Assert.That(timer.Remaining, Is.GreaterThan(0f).And.LessThanOrEqualTo(7f));
        }

        /// <summary>Verifies Throttle Starts Before Result And Retains After Failure.</summary>
        [Test]
        public void ThrottleStartsBeforeResultAndRetainsAfterFailure()
        {
            TimerVariable timer = CreateTimer();
            Throttle node = new() { duration = 10f };
            node.timer.SetRuntimeReference(timer);
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.ReceiveReturnFromChild(false), Is.EqualTo(State.Failed));
            Assert.That(timer.Remaining, Is.GreaterThan(0f));
            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Cooldown other = new();
            other.timer.SetRuntimeReference(timer);
            Assert.That(other.Execute(), Is.EqualTo(State.Failed));
        }

        /// <summary>Verifies Nonpositive Durations Allow Repeated Admission.</summary>
        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonpositiveDurationsAllowRepeatedAdmission(float duration)
        {
            TimerVariable timer = CreateTimer();
            Throttle node = new() { duration = duration };
            node.timer.SetRuntimeReference(timer);
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(timer.Remaining, Is.Zero);
        }

        /// <summary>Verifies Bindings Reject Missing And Ordinary Float Sources.</summary>
        [Test]
        public void BindingsRejectMissingAndOrdinaryFloatSources()
        {
            Cooldown cooldown = new();
            Throttle throttle = new();
            Assert.Throws<InvalidOperationException>(() => cooldown.Initialize());
            Assert.Throws<InvalidOperationException>(() => throttle.Initialize());
            TreeVariable ordinary = new(new VariableData("Ordinary", VariableType.Float));
            cooldown.timer.SetRuntimeReference(ordinary);
            throttle.timer.SetRuntimeReference(ordinary);
            Assert.Throws<InvalidOperationException>(() => cooldown.Initialize());
            Assert.Throws<InvalidOperationException>(() => throttle.Initialize());
            Countdown countdown = new();
            countdown.updatingVariable.SetRuntimeReference(CreateTimer());
            Assert.Throws<InvalidOperationException>(() => countdown.Initialize());
        }

        /// <summary>Verifies Null Child Storage Is Not An Authored Empty Child.</summary>
        [Test]
        public void NullChildStorageIsNotAnAuthoredEmptyChild()
        {
            Cooldown node = new() { node = null };
            TimerVariable timer = CreateTimer();
            node.timer.SetRuntimeReference(timer);
            Assert.That(node.Execute(), Is.EqualTo(State.Error));
            Assert.That(timer.Remaining, Is.Zero);
        }

    }
}
