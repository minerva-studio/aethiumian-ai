using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies background scheduler ownership and one-shot operation publication.</summary>
    public sealed class NavigationPlanningSchedulerTests
    {
        /// <summary>Verifies queued work remains pending until the Map owner starts its workers.</summary>
        [Test]
        public void WorkWaitsForExplicitStart()
        {
            using NavigationPlanningScheduler scheduler = new(1);
            NavigationPlanningOperation operation = scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right));
            Assert.That(operation.IsCompleted, Is.False);
            scheduler.Start();
            WaitForCompletion(operation);
            Assert.That(operation.Result.HasValue, Is.True);
            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
        }

        /// <summary>Verifies cancellation publishes one terminal outcome without invoking queued work.</summary>
        [Test]
        public void QueuedCancellationPublishesCancelledOutcome()
        {
            using CancellationTokenSource cancellation = new();
            using NavigationPlanningScheduler scheduler = new(1);
            int invocationCount = 0;
            TestWork work = new(Vector2.zero, Vector2.right, () => Interlocked.Increment(ref invocationCount));
            NavigationPlanningOperation operation = scheduler.PlanWork(work, cancellation.Token);
            cancellation.Cancel();
            scheduler.Start();
            WaitForCompletion(operation);
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(invocationCount, Is.Zero);
            Assert.That(SpinWait.SpinUntil(() => work.DisposeCount == 1, TimeSpan.FromSeconds(5)), Is.True);
        }

        /// <summary>Verifies cancellation after publication cannot replace a successful result.</summary>
        [Test]
        public void CancellationAfterCompletionDoesNotRewriteResult()
        {
            using CancellationTokenSource cancellation = new();
            using NavigationPlanningScheduler scheduler = new(1);
            NavigationPlanningOperation operation = scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right), cancellation.Token);
            scheduler.Start();
            WaitForCompletion(operation);
            NavigationRoute route = operation.Result;
            cancellation.Cancel();
            Assert.That(operation.IsCancelled, Is.False);
            Assert.That(operation.Result, Is.EqualTo(route));
        }

        /// <summary>Verifies cancellation cannot block while a worker has claimed result publication.</summary>
        [Test]
        public void CancellationDoesNotBlockDuringPreparedPublication()
        {
            using CancellationTokenSource cancellation = new();
            NavigationPlanningOperation operation = new();
            operation.RegisterCancellation(cancellation.Token);
            NavigationRoute route = NavigationRoute.Complete(
                PointGoal(Vector2.right),
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(operation.TryPrepareCompletion(NavigationPlanResult.ResultProduced(route),
                out bool wasCancelled), Is.True);
            Assert.That(wasCancelled, Is.False);

            Task cancel = Task.Run(() => cancellation.Cancel());
            Assert.That(cancel.Wait(TimeSpan.FromSeconds(1)), Is.True,
                "Cancellation callback must not wait on result publication.");
            Assert.That(operation.IsCompleted, Is.False);

            operation.ReleaseCancellationRegistration();
            operation.PublishPreparedCompletion();
            Assert.That(operation.Result, Is.EqualTo(route));
        }

        /// <summary>Verifies a callback-winning cancellation can be retired without self-disposing its registration.</summary>
        [Test]
        public void CallbackCancellationPublishesAndReleasesAtOwnerBoundary()
        {
            using CancellationTokenSource cancellation = new();
            NavigationPlanningOperation operation = new();
            operation.RegisterCancellation(cancellation.Token);

            cancellation.Cancel();

            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(operation.IsCancelled, Is.True);
            operation.ReleaseCancellationRegistration();
            operation.ReleaseCancellationRegistration();
        }

        /// <summary>Verifies planner exceptions are published without escaping the worker thread.</summary>
        [Test]
        public void PlannerExceptionPublishesFailedOutcome()
        {
            using NavigationPlanningScheduler scheduler = new(1);
            NavigationPlanningOperation operation = scheduler.PlanWork(new TestWork(new InvalidOperationException("planner failed")));
            scheduler.Start();
            WaitForCompletion(operation);
            Assert.That(operation.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(operation.Result.HasValue, Is.False);
        }

        /// <summary>Verifies an ordinary null completion is distinguished from cancellation and failure.</summary>
        [Test]
        public void NullCompletionPublishesNoPathOutcome()
        {
            NavigationPlanningOperation operation = new();
            Assert.That(operation.TryComplete(NavigationPlanResult.SearchExhausted()), Is.True);

            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(operation.IsCancelled, Is.False);
            Assert.That(operation.Result.HasValue, Is.False);
            Assert.That(operation.Exception, Is.Null);
            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.SearchExhausted));
        }

        /// <summary>Verifies malformed requests are rejected at the queue boundary.</summary>
        [Test]
        public void QueueRejectsInvalidArguments()
        {
            using NavigationPlanningScheduler scheduler = new(1);
            Assert.Throws<ArgumentNullException>(() => scheduler.PlanWork(null));
            Assert.That(() => new NavigationPlanningScheduler(0), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void SimpleRunsWhileEveryGeneralConsumerIsOccupied()
        {
            using NavigationPlanningScheduler scheduler = new(1024);
            using CountdownEvent entered = new(scheduler.WorkerCount - 1);
            using ManualResetEventSlim release = new(false);
            List<NavigationPlanningOperation> running = new();
            try
            {
                for (int i = 1; i < scheduler.WorkerCount; i++)
                    running.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () =>
                    {
                        entered.Signal();
                        if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    })));
                scheduler.Start();
                scheduler.Start();
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                NavigationPlanningOperation smart = scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right));
                NavigationPlanningOperation simple = scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right),
                    extent: NavigationPlanningExtent.NextAction);
                WaitForCompletion(simple);
                Assert.That(simple.Result.HasValue, Is.True);
                Assert.That(smart.IsCompleted, Is.False, "The reserved consumer must not take Smart work.");
            }
            finally
            {
                release.Set();
                foreach (NavigationPlanningOperation operation in running) WaitForCompletion(operation);
            }
        }

        [Test]
        public void GeneralConsumersHelpSimpleWhenReservedConsumerIsOccupied()
        {
            using NavigationPlanningScheduler scheduler = new(16);
            using CountdownEvent entered = new(2);
            using ManualResetEventSlim release = new(false);
            List<NavigationPlanningOperation> running = new();
            try
            {
                for (int i = 0; i < 2; i++)
                    running.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () =>
                    {
                        entered.Signal();
                        if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    }), extent: NavigationPlanningExtent.NextAction));
                scheduler.Start();
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            }
            finally
            {
                release.Set();
                foreach (NavigationPlanningOperation operation in running) WaitForCompletion(operation);
            }
        }

        [Test]
        public void SmartCannotConsumeReservedWaitingCapacity()
        {
            using NavigationPlanningScheduler scheduler = new(4);
            for (int i = 0; i < 3; i++) scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right));
            TestWork rejected = new(Vector2.zero, Vector2.right);
            Assert.Throws<InvalidOperationException>(() => scheduler.PlanWork(rejected));
            Assert.That(rejected.DisposeCount, Is.EqualTo(1));
            scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right), extent: NavigationPlanningExtent.NextAction);
            Assert.Throws<InvalidOperationException>(() => scheduler.PlanWork(
                new TestWork(Vector2.zero, Vector2.right), extent: NavigationPlanningExtent.NextAction));
        }

        [Test]
        public void CancelledWaitingWorkReleasesCapacityBeforeStart()
        {
            using NavigationPlanningScheduler scheduler = new(1);
            using CancellationTokenSource cancellation = new();
            TestWork cancelled = new(Vector2.zero, Vector2.right);
            scheduler.PlanWork(cancelled, cancellation.Token);
            cancellation.Cancel();
            NavigationPlanningOperation replacement = scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right));
            Assert.That(cancelled.DisposeCount, Is.EqualTo(1));
            scheduler.Start();
            WaitForCompletion(replacement);
            Assert.That(replacement.Result.HasValue, Is.True);
        }

        [Test]
        public void GeneralConsumerUsesThreeToOneOrderAndPreservesLaneOrder()
        {
            using NavigationPlanningScheduler scheduler = new(1024);
            using CountdownEvent entered = new(scheduler.WorkerCount - 1);
            using ManualResetEventSlim releaseOne = new(false);
            using ManualResetEventSlim releaseOthers = new(false);
            using ManualResetEventSlim simpleEntered = new(false);
            List<NavigationPlanningOperation> blockers = new();
            List<NavigationPlanningOperation> requests = new();
            List<int> order = new();
            try
            {
                for (int i = 1; i < scheduler.WorkerCount; i++)
                {
                    ManualResetEventSlim gate = i == 1 ? releaseOne : releaseOthers;
                    blockers.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () =>
                    {
                        entered.Signal();
                        if (!gate.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    })));
                }
                scheduler.Start();
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                blockers.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () =>
                {
                    simpleEntered.Set();
                    if (!releaseOthers.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                }), extent: NavigationPlanningExtent.NextAction));
                Assert.That(simpleEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                for (int i = 0; i < 6; i++)
                {
                    int value = i;
                    requests.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () => order.Add(value)),
                        extent: NavigationPlanningExtent.NextAction));
                }
                for (int i = 0; i < 2; i++)
                {
                    int value = 10 + i;
                    requests.Add(scheduler.PlanWork(new TestWork(Vector2.zero, Vector2.right, () => order.Add(value))));
                }
                releaseOne.Set();
                foreach (NavigationPlanningOperation request in requests) WaitForCompletion(request);
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 10, 3, 4, 5, 11 }, order);
            }
            finally
            {
                releaseOne.Set();
                releaseOthers.Set();
                foreach (NavigationPlanningOperation request in blockers) WaitForCompletion(request);
            }
        }

        [Test]
        public void DisposeBeforeStartRetiresBothLanesAndRejectsSubmission()
        {
            NavigationPlanningScheduler scheduler = new(4);
            TestWork simple = new(Vector2.zero, Vector2.right);
            TestWork smart = new(Vector2.zero, Vector2.right);
            NavigationPlanningOperation first = scheduler.PlanWork(simple, extent: NavigationPlanningExtent.NextAction);
            NavigationPlanningOperation second = scheduler.PlanWork(smart);
            scheduler.Dispose();
            scheduler.Dispose();
            Assert.That(first.IsCancelled && second.IsCancelled, Is.True);
            Assert.That(simple.DisposeCount, Is.EqualTo(1));
            Assert.That(smart.DisposeCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => scheduler.Start());
            using TestWork rejected = new(Vector2.zero, Vector2.right);
            Assert.Throws<ObjectDisposedException>(() => scheduler.PlanWork(rejected));
        }

        [Test]
        public void DisposeCancelsRunningWorkAndReleasesItOnce()
        {
            using ManualResetEventSlim entered = new(false);
            using NavigationPlanningScheduler scheduler = new(4);
            TestWork work = new(Vector2.zero, Vector2.right, tokenCallback: token =>
            {
                entered.Set();
                if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                token.ThrowIfCancellationRequested();
            });
            NavigationPlanningOperation operation = scheduler.PlanWork(work);
            scheduler.Start();
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            scheduler.Dispose();
            WaitForCompletion(operation);
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(SpinWait.SpinUntil(() => work.DisposeCount == 1, TimeSpan.FromSeconds(5)), Is.True);
        }

        [Test]
        public void FailureDrainsBothWaitingLanes()
        {
            using NavigationPlanningScheduler scheduler = new(4);
            TestWork simple = new(Vector2.zero, Vector2.right);
            TestWork smart = new(Vector2.zero, Vector2.right);
            NavigationPlanningOperation first = scheduler.PlanWork(simple, extent: NavigationPlanningExtent.NextAction);
            NavigationPlanningOperation second = scheduler.PlanWork(smart);
            InvalidOperationException failure = new("world failed");
            Assert.That(scheduler.FailPending(failure), Is.EqualTo(2));
            Assert.That(first.Exception, Is.SameAs(failure));
            Assert.That(second.Exception, Is.SameAs(failure));
            Assert.That(simple.DisposeCount, Is.EqualTo(1));
            Assert.That(smart.DisposeCount, Is.EqualTo(1));
        }

        private static NavigationGoalRequest PointGoal(Vector2 point)
            => NavigationGoalRequest.Proximity(AABB.Point(point), DistanceMetric.Euclidean, 0f);

        private static void WaitForCompletion(NavigationPlanningOperation operation)
        {
            Assert.That(SpinWait.SpinUntil(() => operation.IsCompleted, TimeSpan.FromSeconds(5)), Is.True,
                "Background navigation planning did not complete within the bounded test timeout.");
        }

        private sealed class TestWork : INavigationPlanningWork
        {
            private readonly Vector2 start;
            private readonly Vector2 goal;
            private readonly Action callback;
            private readonly Action<CancellationToken> tokenCallback;
            private readonly Exception failure;
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public TestWork(Vector2 start, Vector2 goal, Action callback = null, Action<CancellationToken> tokenCallback = null)
            {
                this.start = start;
                this.goal = goal;
                this.callback = callback;
                this.tokenCallback = tokenCallback;
            }

            public TestWork(Exception failure) => this.failure = failure;

            public NavigationPlanResult Execute(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (failure != null) throw failure;
                callback?.Invoke();
                tokenCallback?.Invoke(cancellationToken);
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(PointGoal(goal),
                    new[] { new GroundRouteSegment(start, goal) }));
            }

            public void Dispose() => Interlocked.Increment(ref disposeCount);
        }
    }
}
