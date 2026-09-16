using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies planner outcomes without prescribing search strategy or route shape.</summary>
    public sealed class NavigationPlannerContractTests
    {
        private static readonly Vector2 Gravity = new(0f, -9.81f);

        [Test]
        public void WalkPlanner_ReachableGoalProducesACompletingRoute()
        {
            TestNavigationWorld world = NavigationTestWorlds.Ground(0, 6);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = BindGoal(new Vector2(5.5f, 1f));

            NavigationPlanResult result = new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world))
                .Plan(new Vector2(0.5f, 1f), goal, WalkParameters(bodySize));

            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(result.Route.Segments, Is.Not.Empty);
            Assert.That(world.IsGoalComplete(goal, result.Route.ResolvedGoal + Vector2.up * (bodySize.y * 0.5f), bodySize), Is.True);
        }

        [Test]
        public void FlyPlanner_UnreachableGoalReportsNoPath()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = BindGoal(new Vector2(10.5f, 0.5f));

            NavigationPlanResult result = new FlyNavigationPlanner(world, 16)
                .Plan(new Vector2(0.5f, 0.5f), goal, new FlyNavigationParameters(new Vector2(0.8f, 0.8f)));

            // An unreachable goal can be rejected before the search frontier is built;
            // both the no-result termination and a null route are the public no-path contract.
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(result.Route, Is.Null);
        }

        [Test]
        public void Planner_RejectsNonFiniteStartAtItsPublicBoundary()
        {
            TestNavigationWorld world = NavigationTestWorlds.Ground(0, 2);
            Assert.That(() => new WalkNavigationPlanner(world, 16, new GroundJumpSolver(world)).Plan(
                new Vector2(float.NaN, 1f), BindGoal(new Vector2(1.5f, 1f)), WalkParameters(new Vector2(0.8f, 1.5f))),
                Throws.ArgumentException);
        }

        [Test]
        public void WorldOutsideBoundsRejectsBodyClearance()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 2, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Assert.That(world.IsBodyClear(new Rect(-0.1f, 0.5f, 0.2f, 0.2f), 0f), Is.False);
            Assert.That(world.IsBodyClear(new Rect(0.5f, 0.5f, 0.2f, 0.2f), 0f), Is.True);
        }

        [Test]
        public void WorldResolvesAuthoredSolidSupportHeightWithoutCellProjection()
        {
            Vector2Int[] floor = { new(0, 5), new(1, 5), new(2, 5) };
            Dictionary<Vector2Int, float> heights = new()
            {
                [floor[0]] = 5.38f, [floor[1]] = 5.38f, [floor[2]] = 5.38f,
            };
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 8), floor, Array.Empty<Vector2Int>(), heights);
            Assert.That(world.TryResolveSupport(new Vector2(1.5f, 5.38f), new Vector2(0.8f, 1.5f),
                NavigationWorldQueries.SupportSnapDistance, out NavigationSupport support), Is.True);
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.Solid));
            Assert.That(support.Position.y, Is.EqualTo(5.38f).Within(0.0001f));
        }

        [Test]
        public void GroundFixtureProvidesWorldSpaceGeometricSupport()
        {
            TestNavigationWorld world = NavigationTestWorlds.Ground(0, 1);
            Assert.That(world.TryResolveSupport(new Vector2(0.5f, 1f), new Vector2(0.8f, 1.5f),
                NavigationWorldQueries.SupportSnapDistance, out NavigationSupport support), Is.True);
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.Solid));
            Assert.That(support.Position.y, Is.EqualTo(1f));
        }

        private static NavigationGoalRequest BindGoal(Vector2 center)
            => NavigationGoalRequest.Proximity(new AABB(center, center), DistanceMetric.Euclidean, 0.1f);

        private static WalkNavigationParameters WalkParameters(Vector2 bodySize)
            => new(bodySize, 5f, Gravity, 1f, 0f, 2f, 4f, 0.02f);
    }
}
