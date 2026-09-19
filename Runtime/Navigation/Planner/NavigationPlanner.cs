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
        protected const float Tolerance = NavigationConstant.Epsilon;


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
        /// Runs planning synchronously against this planner's immutable navigation world. The body AABB
        /// carries the caller's pose: each planner derives its own mode-specific start anchor from it
        /// (Walk and Jump from the lower center, Fly from the center), so no caller ever supplies a
        /// separate start vector. Each planner also derives the collision size from this same body snapshot.
        /// </summary>
        public abstract NavigationPlanResult Plan(AABB body, NavigationGoalRequest goal, TParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null);

        /// <summary>
        /// Attempts to create the first executable route produced by this planner.
        /// </summary>
        public bool TryPlan(AABB body, NavigationGoalRequest goal, TParameters parameters, out NavigationRoute route)
        {
            route = Plan(body, goal, parameters).Route;
            return route != null;
        }

        /// <summary>
        /// Runs a single-step planning request, which may be a direct connection or a local neighbour expansion. 
        /// </summary>
        /// <param name="body"></param>
        /// <param name="goal"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual NavigationPlanResult PlanSingleStep(AABB body, NavigationGoalRequest goal, TParameters parameters, CancellationToken cancellationToken = default)
        {
            return NavigationPlanResult.NoResult;
        }

        /// <summary>
        /// Validates the request-owned common input before mode-specific planning. This planner owns
        /// the World used by its geometry queries; the produced route carries only the resulting goal
        /// and segment chain. External derived planners should invoke this at the start of their
        /// <see cref="Plan"/> override.
        /// </summary>
        protected void ValidatePlanInputs(AABB body, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate.PositiveAabb(body, nameof(body));
        }

        /// <summary>
        /// Advances the shared action-graph search with the planner's standard cooperative budget.
        /// A complete-route request publishes only a route that reaches its planning goal.
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

                if (update.Status == NavigationSearchStatus.CompleteRoute)
                    return NavigationPlanResult.ResultProduced(update.Route);

                if (update.Status == NavigationSearchStatus.Exhausted)
                    return NavigationPlanResult.SearchExhausted();

                if (update.Status == NavigationSearchStatus.BudgetReached)
                    return NavigationPlanResult.BudgetReached();
            }
        }

        /// <summary>Compares planner distances with the shared strict-improvement tolerance.</summary>
        protected static bool IsStrictlyLess(float candidate, float reference) => candidate < reference - Tolerance;

        /// <summary>Returns whether two planner distances are equivalent within the shared tolerance.</summary>
        protected static bool IsEquivalent(float first, float second) => first.Equals(second) || (!float.IsNaN(first) && !float.IsNaN(second) && Mathf.Abs(first - second) <= Tolerance);
    }
}
