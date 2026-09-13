using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Provides the shared planning boundary for navigation modes while leaving route generation
    /// and mode-specific parameter validation to derived planners.
    /// </summary>
    public abstract class NavigationPlanner<TParameters>
    {
        protected const float Tolerance = 0.0001f;


        protected NavigationPlanner(INavigationWorld world, int maxExpandedNodes)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (maxExpandedNodes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));

            MaxExpandedNodes = maxExpandedNodes;
        }

        /// <summary>Gets the immutable navigation world used by every request for this planner.</summary>
        protected INavigationWorld World { get; }

        /// <summary>Gets the maximum number of search nodes this planner may expand per request.</summary>
        protected int MaxExpandedNodes { get; }

        /// <summary>
        /// Runs planning synchronously against this planner's immutable navigation world.
        /// </summary>
        public abstract NavigationPlanResult Plan(Vector2 start, NavigationGoalRegion goalRegion, TParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null, bool allowExecutablePrefix = false);

        /// <summary>
        /// Attempts to create the first executable route produced by this planner.
        /// </summary>
        public bool TryPlan(Vector2 start, NavigationGoalRegion goalRegion, TParameters parameters, out NavigationRoute route)
        {
            route = Plan(start, goalRegion, parameters).Route;
            return route != null;
        }

        /// <summary>
        /// Runs a single-step planning request, which may be a direct connection or a local neighbour expansion. 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="goal"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual NavigationPlanResult PlanSingleStep(Vector2 start, NavigationGoalRegion goal, TParameters parameters, CancellationToken cancellationToken = default)
        {
            return NavigationPlanResult.NoResult;
        }

        /// <summary>
        /// Validates the stable world binding and request-owned common input before mode-specific planning.
        /// External derived planners should invoke this at the start of their <see cref="Plan"/> override.
        /// </summary>
        protected void ValidatePlanInputs(Vector2 start, NavigationGoalRegion goalRegion, CancellationToken cancellationToken)
        {
            if (goalRegion == null)
                throw new ArgumentNullException(nameof(goalRegion));
            if (!ReferenceEquals(goalRegion.Snapshot, World))
                throw new ArgumentException("The goal region must be bound to this planner's navigation world.", nameof(goalRegion));

            cancellationToken.ThrowIfCancellationRequested();
            Validate.Finite(start, nameof(start));
        }

        /// <summary>
        /// Advances the shared action-graph search with the planner's standard cooperative budget.
        /// This remains assembly-scoped because <see cref="NavigationSearchRequest"/> is internal;
        /// external planners implement their own <see cref="Plan"/> method instead.
        /// </summary>
        protected static NavigationPlanResult RunSearch(NavigationSearchRequest request, NavigationPlanningDiagnostics diagnostics, CancellationToken cancellationToken)
        {
            using NavigationSearch search = new(request);
            NavigationWorkBudget budget = new(64, 2d);
            int recordedExpansions = 0;
            while (true)
            {
                NavigationSearchUpdate update = search.Advance(budget, cancellationToken);
                while (recordedExpansions < update.ExpandedNodes)
                {
                    diagnostics?.RecordPathExpansion();
                    recordedExpansions++;
                }

                if (update.Status == NavigationSearchStatus.CompleteRoute
                    || update.Status == NavigationSearchStatus.ExecutablePrefix
                    || update.Status == NavigationSearchStatus.Exhausted && update.Route != null)
                {
                    return update.Status == NavigationSearchStatus.Exhausted
                        ? NavigationPlanResult.SearchExhausted(update.Route)
                        : NavigationPlanResult.ResultProduced(update.Route);
                }

                if (update.Status == NavigationSearchStatus.Exhausted)
                    return NavigationPlanResult.SearchExhausted();

                if (update.Status == NavigationSearchStatus.BudgetReached && update.IsTerminal)
                {
                    return NavigationPlanResult.BudgetReached(update.Route);
                }
            }
        }

        /// <summary>Compares planner distances with the shared strict-improvement tolerance.</summary>
        protected static bool IsStrictlyLess(float candidate, float reference) => candidate < reference - Tolerance;

        /// <summary>Returns whether two planner distances are equivalent within the shared tolerance.</summary>
        protected static bool IsEquivalent(float first, float second) => first.Equals(second) || (!float.IsNaN(first) && !float.IsNaN(second) && Mathf.Abs(first - second) <= Tolerance);
    }
}
