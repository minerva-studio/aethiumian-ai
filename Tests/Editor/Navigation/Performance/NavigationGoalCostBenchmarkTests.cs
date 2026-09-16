using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Measures the cost of the Navigation Goal contract: per-operation goal geometry, the ABI cost of
    /// passing <see cref="NavigationGoalRequest"/> by value, and the end-to-end cost of a real planner search.
    /// <para>
    /// Two measurement caveats this fixture deliberately encodes:
    /// the control row exposes the framework and loop floor, so every other row can be read net of it;
    /// and no GC column is reported, because the framework's <c>.GC()</c> recorder sums the whole Editor
    /// frame (not just the measured synchronous Action), and <c>GC.GetTotalAllocatedBytes</c> does not exist
    /// on this Editor's Mono. Allocation of the goal path is a code-level fact, not a sampled one.
    /// </para>
    /// These are Editor/Mono numbers: the Editor does not inline these small methods, so per-operation rows
    /// are call-overhead bound and are an upper bound, not a player-build cost.
    /// Runs on demand, never in the normal gate.
    /// </summary>
    public sealed class NavigationGoalCostBenchmarkTests
    {
        /// <summary>Operations per measured sample, so per-call overhead of the harness is amortized.</summary>
        private const int Batch = 10_000;
        private const int MeasurementCount = 16;
        private const int WarmupCount = 4;
        private static readonly Vector2 Gravity = new(0f, -9.81f);
        private static readonly Vector2 BodySize = new(0.8f, 1.5f);
        private static readonly Vector2 PlanStart = new(0.5f, 1f);
        private static readonly Vector2 PlanGoalPoint = new(60.5f, 1f);
        private static readonly Vector2 SampleCenter = new(30.5f, 1.75f);
        private static readonly Vector2 SweepStart = new(30.5f, 1.75f);
        private static readonly Vector2 SweepEnd = new(31.5f, 1.75f);

        // Keeps measured work observable so the JIT cannot delete the loops.
        private static float sink;

        // Four probes with byte-identical bodies: only the call shape (by value vs in) and the inlining
        // attribute differ, so the 2x2 isolates each factor. The varying bias keeps the loop from being
        // folded away while the struct argument stays loop invariant, as it is in the planner's inner loop.
        // Measured caveat: this Editor's Mono ignores AggressiveInlining for these probes (the inlined and
        // NoInlining rows time identically), so the two "Inlined" rows do NOT establish that inlining
        // removes the by-value copy. Confirming that needs a player/IL2CPP build.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float RequestByValue(NavigationGoalRequest goal, float bias)
            => goal.ArrivalTolerance + bias;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float RequestByIn(in NavigationGoalRequest goal, float bias)
            => goal.ArrivalTolerance + bias;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RequestByValueInlined(NavigationGoalRequest goal, float bias)
            => goal.ArrivalTolerance + bias;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RequestByInInlined(in NavigationGoalRequest goal, float bias)
            => goal.ArrivalTolerance + bias;

        /// <summary>Per-operation cost of the goal entry points the planner calls for every successor.</summary>
        [Test, Performance]
        [Explicit("Performance diagnostic: run on demand")]
        public void GoalContract_PerOperationCost()
        {
            TestNavigationWorld world = CreateOpenWorld();
            NavigationGoalRequest groundGoal = NavigationGoalRequest.GroundRange(AABB.Point(PlanGoalPoint), 0.1f);
            NavigationGoalRequest centeredGoal = NavigationGoalRequest.Proximity(
                AABB.Point(PlanGoalPoint), DistanceMetric.Euclidean, 0.5f);

            RunBatch("Control.EmptyLoop", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += 1f;
                });
            RunBatch("Goal.IsGoalComplete.GroundRange", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += world.IsGoalComplete(groundGoal, SampleCenter, BodySize) ? 1f : 0f;
                });
            RunBatch("Goal.IsGoalCompleteAlong.Sweep", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += world.IsGoalCompleteAlong(groundGoal, SweepStart, SweepEnd, BodySize) ? 1f : 0f;
                });
            RunBatch("Goal.GetGoalCompletionDistance.Proximity", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += world.GetGoalCompletionDistance(centeredGoal, SampleCenter, BodySize);
                });
            RunBatch("Goal.GuidanceDistance", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += groundGoal.GuidanceDistance(SampleCenter, BodySize);
                });

            LogRows(Batch, "per-operation goal cost over " + Batch + " ops per sample (ns/op, Editor/Mono upper bound)",
                "Control.EmptyLoop",
                "Goal.IsGoalComplete.GroundRange",
                "Goal.IsGoalCompleteAlong.Sweep",
                "Goal.GetGoalCompletionDistance.Proximity",
                "Goal.GuidanceDistance");
            LogDelta(Batch, "Goal.IsGoalComplete.GroundRange", "Control.EmptyLoop",
                "IsGoalComplete.GroundRange net of the control floor");
            LogDelta(Batch, "Goal.IsGoalCompleteAlong.Sweep", "Control.EmptyLoop",
                "IsGoalCompleteAlong.Sweep net of the control floor");
        }

        /// <summary>
        /// ABI cost of the two call shapes for the 36-byte request struct. Both sides are NoInlining, so the
        /// difference is the copy the by-value ABI really performs, not inlining differences.
        /// </summary>
        [Test, Performance]
        [Explicit("Performance diagnostic: run on demand")]
        public void GoalContract_RequestCopyShape()
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(PlanGoalPoint), 0.1f);

            RunBatch("Goal.Copy.ByValue", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += RequestByValue(goal, index & 1);
                });
            RunBatch("Goal.Copy.InParameter", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += RequestByIn(goal, index & 1);
                });
            RunBatch("Goal.Copy.ByValueInlined", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += RequestByValueInlined(goal, index & 1);
                });
            RunBatch("Goal.Copy.InParameterInlined", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += RequestByInInlined(goal, index & 1);
                });

            LogRows(Batch, "request struct call shape (ns/call; the first two are NoInlining, the last two inlinable)",
                "Goal.Copy.ByValue",
                "Goal.Copy.InParameter",
                "Goal.Copy.ByValueInlined",
                "Goal.Copy.InParameterInlined");
            LogDelta(Batch, "Goal.Copy.ByValue", "Goal.Copy.InParameter",
                "worst-case by-value penalty over in-parameter (no inlining)");
            LogDelta(Batch, "Goal.Copy.ByValueInlined", "Goal.Copy.InParameterInlined",
                "by-value penalty over in-parameter once the callee is inlined");
        }

        /// <summary>
        /// End-to-end cost of a real Smart Walk search. The floor has a three-cell gap, so the planner must
        /// expand and validate jump successors instead of taking the direct-route fast path. The world is a
        /// test double, so the per-plan figure is a workload-relative number, not a production map cost.
        /// </summary>
        [Test, Performance]
        [Explicit("Performance diagnostic: run on demand")]
        public void GoalContract_RealPlannerRun()
        {
            TestNavigationWorld world = CreateGappedWorld();
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(PlanGoalPoint), 0.1f);
            WalkNavigationParameters withJumps = new(BodySize, 5f, Gravity, 1f, 0f, 2f, 4f, 0.02f);
            WalkNavigationParameters groundOnly = new(BodySize, 5f, Gravity, 1f, 0f, 0f, 0f, 0.02f);

            Measure.Method(() => RunPlan(world, goal, withJumps, out _))
                .SampleGroup("Plan.Walk.GappedWithJumps")
                .WarmupCount(3).MeasurementCount(10).IterationsPerMeasurement(1).Run();
            Measure.Method(() => RunPlan(world, goal, groundOnly, out _))
                .SampleGroup("Plan.Walk.GappedGroundOnly")
                .WarmupCount(3).MeasurementCount(10).IterationsPerMeasurement(1).Run();

            RunPlan(world, goal, withJumps, out NavigationPlanningDiagnostics jumpWorkload);
            RunPlan(world, goal, groundOnly, out NavigationPlanningDiagnostics groundWorkload);
            LogRows(1, "real WalkNavigationPlanner.Plan across a three-cell gap (ms per plan incl. warm structures)",
                "Plan.Walk.GappedWithJumps",
                "Plan.Walk.GappedGroundOnly");
            Debug.Log("[GoalCost] withJumps workload: expansions=" + jumpWorkload.ExpansionCount
                + ", jumpCandidatesGenerated=" + jumpWorkload.JumpCandidateGeneratedCount
                + ", jumpCandidatesValidated=" + jumpWorkload.JumpCandidateValidatedCount
                + ", terminalCandidates=" + jumpWorkload.TerminalCandidateCount
                + " | groundOnly workload: expansions=" + groundWorkload.ExpansionCount
                + ", jumpCandidatesGenerated=" + groundWorkload.JumpCandidateGeneratedCount
                + ", jumpCandidatesValidated=" + groundWorkload.JumpCandidateValidatedCount
                + ", terminalCandidates=" + groundWorkload.TerminalCandidateCount);
        }

        private static void RunBatch(string sampleGroup, System.Action action)
        {
            Measure.Method(action)
                .SampleGroup(sampleGroup)
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).IterationsPerMeasurement(1).Run();
        }

        private static void RunPlan(INavigationWorld world, NavigationGoalRequest goal,
            WalkNavigationParameters parameters, out NavigationPlanningDiagnostics diagnostics)
        {
            diagnostics = new NavigationPlanningDiagnostics();
            WalkNavigationPlanner planner = new(world, 256, new GroundJumpSolver(world));
            NavigationPlanResult result = planner.Plan(PlanStart, goal, parameters, default, diagnostics);
            sink += result.Route == null ? 0f : result.Route.Count;
        }

        private static void LogRows(int operationsPerSample, string header, params string[] names)
        {
            StringBuilder report = new StringBuilder();
            report.Append("[GoalCost] ").AppendLine(header);
            foreach (string name in names)
            {
                double? median = Median(name);
                report.Append("[GoalCost]   ").Append(name.PadRight(38))
                    .Append(median.HasValue
                        ? (median.Value * 1e6 / operationsPerSample).ToString("F3") + " ns/unit"
                        : "n/a")
                    .AppendLine();
            }

            report.Append("[GoalCost] sink=").Append(sink.ToString("F1"));
            Debug.Log(report.ToString());
        }

        private static void LogDelta(int operationsPerSample, string baseline, string reference, string label)
        {
            double? baselineMedian = Median(baseline);
            double? referenceMedian = Median(reference);
            if (!baselineMedian.HasValue || !referenceMedian.HasValue) return;
            Debug.Log("[GoalCost] " + label + " = "
                + ((baselineMedian.Value - referenceMedian.Value) * 1e6 / operationsPerSample).ToString("F3")
                + " ns/unit");
        }

        /// <summary>
        /// Reads the recorded samples directly. With an explicit MeasurementCount the framework stores the
        /// total elapsed time of one measured sample (it does not average unless it probes for the count),
        /// and it only fills Median/Average at test teardown, so this fixture summarizes raw samples itself.
        /// </summary>
        private static double? Median(string name)
        {
            IReadOnlyList<SampleGroup> groups = PerformanceTest.Active?.SampleGroups;
            if (groups == null) return null;
            for (int index = 0; index < groups.Count; index++)
            {
                SampleGroup group = groups[index];
                if (group == null || group.Name != name || group.Samples == null || group.Samples.Count == 0)
                    continue;
                List<double> sorted = new(group.Samples);
                sorted.Sort();
                int middle = sorted.Count / 2;
                return sorted.Count % 2 == 1
                    ? sorted[middle]
                    : (sorted[middle - 1] + sorted[middle]) * 0.5;
            }
            return null;
        }

        private static TestNavigationWorld CreateOpenWorld() => NavigationTestWorlds.Ground(0, 64);

        private static TestNavigationWorld CreateGappedWorld()
        {
            List<Vector2Int> floor = NavigationTestWorlds.Floor(0, 31);
            floor.AddRange(NavigationTestWorlds.Floor(34, 30));
            return new TestNavigationWorld(new RectInt(0, 0, 64, 8), floor, System.Array.Empty<Vector2Int>());
        }
    }
}
