using Aethiumian.AI.Navigation;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Verifies lifecycle ownership and watchdog behavior shared by movement executors.</summary>
    public sealed class MovementExecutorTests
    {
        private sealed class Probe : MovementExecutor
        {
            public readonly List<int> Released = new();
            public ExecutionResult Next = ExecutionResult.Running;
            public bool Waiting;
            public Exception TickException;
            public bool ThrowOnRelease;
            private int resource;

            public Probe(float maximumIdleDuration = 0f) : base(maximumIdleDuration) { }
            public void Prepare(int nextResource) { BeginExecution(); resource = nextResource; }
            public void Cancel() => CancelExecution();
            public void Reset() => ResetProgressBaseline();

            protected override ExecutionResult Tick_Internal(float deltaTime)
            {
                if (TickException != null) throw TickException;
                return Next;
            }

            protected override ProgressObservation ObserveProgress()
                => Waiting ? ProgressObservation.Waiting : ProgressObservation.Advanced;

            protected override void ReleaseExecutionResources()
            {
                Released.Add(resource);
                if (ThrowOnRelease) throw new ApplicationException("Cleanup failure");
            }
        }

        [Test]
        public void InactiveAndDisposedCallsAreRejected()
        {
            var executor = new Probe();
            Assert.Throws<InvalidOperationException>(() => executor.Tick(0.02f));
            executor.Prepare(1);
            executor.Next = ExecutionResult.Completed;
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Completed));
            Assert.Throws<InvalidOperationException>(() => executor.Tick(0.02f));
            executor.Dispose();
            Assert.Throws<ObjectDisposedException>(() => executor.Tick(0.02f));
            Assert.Throws<ObjectDisposedException>(() => executor.Prepare(2));
            Assert.That(executor.Released, Is.EqualTo(new[] { 1 }));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTimeDoesNotAdvanceOrCancel(float deltaTime)
        {
            using var executor = new Probe();
            executor.Prepare(1);
            Assert.That(() => executor.Tick(deltaTime), Throws.InstanceOf<ArgumentException>());
            Assert.That(executor.Released, Is.Empty);
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Running));
        }

        [Test]
        public void RunningResultKeepsExecutionActive()
        {
            using var executor = new Probe { Next = ExecutionResult.Running };
            executor.Prepare(1);
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Running));
            Assert.That(executor.Released, Is.Empty);
        }

        [Test]
        public void FailureIsTerminalAndAllowsReuse()
        {
            var executor = new Probe { Next = ExecutionResult.Failure(ExecutionFailureReason.Obstructed) };
            executor.Prepare(1);
            ExecutionResult result = executor.Tick(0.02f);
            Assert.That(result.Status, Is.EqualTo(ExecutionStatus.Failed));
            Assert.That(result.FailureReason, Is.EqualTo(ExecutionFailureReason.Obstructed));
            Assert.Throws<InvalidOperationException>(() => executor.Tick(0.02f));
            executor.Next = ExecutionResult.Completed;
            executor.Prepare(2);
            executor.Tick(0.02f);
            executor.Dispose();
            Assert.That(executor.Released, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void ReplacementReleasesOldResourceBeforeInstallingNewResource()
        {
            var executor = new Probe();
            executor.Prepare(1);
            executor.Prepare(2);
            Assert.That(executor.Released, Is.EqualTo(new[] { 1 }));
            executor.Cancel();
            executor.Cancel();
            executor.Dispose();
            Assert.That(executor.Released, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void WatchdogFailsWaitingActionAtThreshold()
        {
            using var executor = new Probe(0.1f) { Waiting = true };
            executor.Prepare(1);
            Assert.That(executor.Tick(0.05f).Status, Is.EqualTo(ExecutionStatus.Running));
            ExecutionResult result = executor.Tick(0.06f);
            Assert.That(result.Status, Is.EqualTo(ExecutionStatus.Failed));
            Assert.That(result.FailureReason, Is.EqualTo(ExecutionFailureReason.Stalled));
            Assert.Throws<InvalidOperationException>(() => executor.Tick(0.01f));
            Assert.That(executor.Released, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ProgressAndBaselineResetClearWatchdogTimer()
        {
            using var executor = new Probe(0.1f) { Waiting = true };
            executor.Prepare(1);
            Assert.That(executor.Tick(0.08f).Status, Is.EqualTo(ExecutionStatus.Running));
            executor.Reset();
            Assert.That(executor.Tick(0.08f).Status, Is.EqualTo(ExecutionStatus.Running));
            executor.Waiting = false;
            Assert.That(executor.Tick(0.02f).Status, Is.EqualTo(ExecutionStatus.Running));
        }

        [Test]
        public void ResultProtocolRejectsFailedWithoutReason()
        {
            Assert.That(() => ExecutionResult.Failure(ExecutionFailureReason.None),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(ExecutionResult.Completed.FailureReason, Is.EqualTo(ExecutionFailureReason.None));
            Assert.That(ExecutionResult.Running.FailureReason, Is.EqualTo(ExecutionFailureReason.None));
        }

        [Test]
        public void ExecutionExceptionReleasesAndPreservesOriginalException()
        {
            var error = new ApplicationException("Execution failure");
            var executor = new Probe { TickException = error, ThrowOnRelease = true };
            executor.Prepare(1);
            Assert.That(Assert.Throws<ApplicationException>(() => executor.Tick(0.02f)), Is.SameAs(error));
            Assert.Throws<InvalidOperationException>(() => executor.Tick(0.02f));
            executor.Dispose();
            Assert.That(executor.Released, Is.EqualTo(new[] { 1 }));
        }
    }
}
