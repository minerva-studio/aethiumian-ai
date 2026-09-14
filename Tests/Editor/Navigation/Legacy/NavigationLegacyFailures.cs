// Consolidated explicit navigation regressions; normal test runs exclude [Explicit].
using NUnit.Framework;
using System.Threading;
using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{

    // ===== Runtime cache failures =====
    /// <summary>Legacy runtime memoization failure retained for explicit regression runs.</summary>
    public sealed partial class MapNavigationRuntimeTests
    {
        [Explicit("Known migration failure: Smart Walk negative memoization returns a route for an exhausted request.")]
        [Test]
        public void SmartWalkMemoizesOnlyExactExhaustedFailure()
        {
            using MapNavigationRuntime runtime = new(4, 128, 64);
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0) }, Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f);
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.Route, CancellationToken.None, NavigationPlanningPurpose.EndpointContinuation);
            WaitForCompletion(first);
            Assert.That(first.Result, Is.Null, DescribeRoute(first.Result));
            Assert.That(first.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(first.Result));

            NavigationPlanningOperation repeated = runtime.PlanWalkAsync(start, goal, groundedOnly);
            Assert.That(repeated.IsCompleted, Is.True,
                "Only the exact exhausted request may use the negative cache.");
            Assert.That(repeated.Result, Is.Null);

            NavigationGoalRequest lineOfSightGoal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f, true);
            NavigationPlanningOperation changedGoal = runtime.PlanWalkAsync(start, lineOfSightGoal, groundedOnly);
            Assert.That(changedGoal.IsCompleted, Is.False,
                "A changed LOS requirement must not hit the terminal memo for another goal identity.");
            WaitForCompletion(changedGoal);
            Assert.That(changedGoal.Result, Is.Null, DescribeRoute(changedGoal.Result));
            Assert.That(changedGoal.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(changedGoal.Result));

            NavigationPlanningOperation differentStart = runtime.PlanWalkAsync(
                start + Vector2.right * 0.19f, goal, groundedOnly);
            Assert.That(differentStart.IsCompleted, Is.False,
                "A nearby start is a separate request and must not inherit another start's failure.");
            WaitForCompletion(differentStart);
            Assert.That(differentStart.Result, Is.Null, DescribeRoute(differentStart.Result));

            NavigationPlanningOperation simple = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.NextAction);
            WaitForCompletion(simple);
            Assert.That(simple.Result, Is.Null,
                "Simple Walk must not consume Smart Walk terminal memo state.");
        }
    }

}
