using System;
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
            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.Pending));
            scheduler.Start();
            WaitForCompletion(operation);
            Assert.That(operation.Result, Is.Not.Null);
            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.RouteFound));
        }

        /// <summary>Verifies cancellation publishes one terminal outcome without invoking queued work.</summary>
        [Test]
        public void QueuedCancellationPublishesCancelledOutcome()
        {
            using CancellationTokenSource cancellation = new();
            using NavigationPlanningScheduler scheduler = new(1);
            int invocationCount = 0;
            NavigationPlanningOperation operation = scheduler.PlanWork(
                new TestWork(Vector2.zero, Vector2.right, () => Interlocked.Increment(ref invocationCount)), cancellation.Token);
            cancellation.Cancel();
            scheduler.Start();
            WaitForCompletion(operation);
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.Cancelled));
            Assert.That(invocationCount, Is.Zero);
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
            Assert.That(operation.Result, Is.SameAs(route));
        }

        /// <summary>Verifies cancellation cannot block while a worker has claimed result publication.</summary>
        [Test]
        public void CancellationDoesNotBlockDuringPreparedPublication()
        {
            using CancellationTokenSource cancellation = new();
            NavigationPlanningOperation operation = new();
            operation.RegisterCancellation(cancellation.Token);
            NavigationRoute route = NavigationRoute.Complete(Vector2.zero,
                BoundPoint(Vector2.right), Vector2.right,
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
            Assert.That(operation.Result, Is.SameAs(route));
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
            Assert.That(operation.Result, Is.Null);
            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.Faulted));
        }

        /// <summary>Verifies an ordinary null completion is distinguished from cancellation and failure.</summary>
        [Test]
        public void NullCompletionPublishesNoPathOutcome()
        {
            NavigationPlanningOperation operation = NavigationPlanningOperation.CreateSearchExhausted();

            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(operation.IsCancelled, Is.False);
            Assert.That(operation.Result, Is.Null);
            Assert.That(operation.Exception, Is.Null);
            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));
        }

        /// <summary>Verifies malformed requests are rejected at the queue boundary.</summary>
        [Test]
        public void QueueRejectsInvalidArguments()
        {
            using NavigationPlanningScheduler scheduler = new(1);
            Assert.Throws<ArgumentNullException>(() => scheduler.PlanWork(null));
            Assert.That(() => new NavigationPlanningScheduler(0), Throws.InstanceOf<ArgumentException>());
        }

        private static NavigationGoalRegion BoundPoint(Vector2 point)
            => NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(point, Vector3.zero), DistanceMetric.Euclidean, 0f),
                new TestNavigationWorld(new RectInt(-4, -4, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>()));

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
            private readonly Exception failure;

            public TestWork(Vector2 start, Vector2 goal, Action callback = null)
            {
                this.start = start;
                this.goal = goal;
                this.callback = callback;
            }

            public TestWork(Exception failure) => this.failure = failure;

            public NavigationPlanResult Execute(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (failure != null) throw failure;
                callback?.Invoke();
                NavigationGoalRegion region = BoundPoint(goal);
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, region, goal,
                    new[] { new GroundRouteSegment(start, goal) }));
            }

            public void Dispose() { }
        }
    }
}
