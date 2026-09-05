using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Verifies the tree-owned timer contract without injecting a synthetic clock.</summary>
    public sealed class TimeSettingsTests
    {
        [Test]
        public void TimeSettingsDefaultsToGameScaled()
        {
            Assert.That(new TimeSettings().domain, Is.EqualTo(TimeDomain.Game));
            Assert.That(new TimeSettings().scaleMode, Is.EqualTo(TimeScaleMode.Scaled));
        }

        [Test]
        public void TimerStartsIdleAndReadsDoNotStartIt()
        {
            TimerVariable timer = CreateTimer(new TimeSettings { domain = TimeDomain.Game, scaleMode = TimeScaleMode.Unscaled });
            Assert.That(timer.Remaining, Is.Zero);
            Assert.That(timer.Remaining, Is.Zero);
        }

        [Test]
        public void TimerRejectsNonFiniteDurationAndClearsOnNonPositiveWrite()
        {
            TimerVariable timer = CreateTimer(default);
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.SetValue(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.SetValue(float.PositiveInfinity));
            timer.SetValue(0f);
            Assert.That(timer.Remaining, Is.Zero);
            timer.SetValue(-1f);
            Assert.That(timer.Remaining, Is.Zero);
        }

        [Test]
        public void CountdownAcceptsOrdinaryGlobalFloatAndWritableScriptFloat()
        {
            VariableData globalData = new("Global", VariableType.Float) { IsGlobal = true };
            TreeVariable global = new(globalData);
            Countdown globalCountdown = new();
            globalCountdown.updatingVariable.SetRuntimeReference(global);
            Assert.DoesNotThrow(globalCountdown.Initialize);

            ScriptValues values = new();
            VariableData writableData = new("Writable", VariableType.Float) { Path = nameof(ScriptValues.Writable) };
            TargetScriptVariable writable = new(writableData, values);
            Countdown writableCountdown = new();
            writableCountdown.updatingVariable.SetRuntimeReference(writable);
            Assert.DoesNotThrow(writableCountdown.Initialize);
        }

        [Test]
        public void CountdownRejectsReadOnlyScriptFloat()
        {
            ScriptValues values = new();
            VariableData data = new("ReadOnly", VariableType.Float) { Path = nameof(ScriptValues.ReadOnly) };
            TargetScriptVariable readOnly = new(data, values);
            Countdown countdown = new();
            countdown.updatingVariable.SetRuntimeReference(readOnly);
            Assert.Throws<InvalidOperationException>(countdown.Initialize);
        }

        private static TimerVariable CreateTimer(TimeSettings settings)
        {
            return new TimerVariable(new BehaviourTreeTimer(settings), UUID.NewUUID(), "Timer");
        }

        private sealed class ScriptValues
        {
            public float Writable { get; set; } = 2f;
            public float ReadOnly => 2f;
        }
    }
}
