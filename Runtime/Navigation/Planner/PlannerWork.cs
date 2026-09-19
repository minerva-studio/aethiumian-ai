using System;
using System.Threading;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Provides one-shot ownership and cleanup for detached planner work.</summary>
    internal abstract class PlannerWork<TPlanner> : INavigationPlanningWork where TPlanner : class
    {
        private TPlanner planner;

        protected PlannerWork(TPlanner planner)
        {
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        /// <summary>Consumes the planner reference and executes this request at most once.</summary>
        public NavigationPlanResult Execute(CancellationToken cancellationToken)
        {
            TPlanner ownedPlanner = Interlocked.Exchange(ref planner, null);
            if (ownedPlanner == null) throw new InvalidOperationException("Planning work was already executed or disposed.");
            cancellationToken.ThrowIfCancellationRequested();
            return ExecutePlanner(ownedPlanner, cancellationToken);
        }

        /// <summary>Releases an unexecuted planner reference when queued work is cancelled.</summary>
        public void Dispose() => Interlocked.Exchange(ref planner, null);

        protected abstract NavigationPlanResult ExecutePlanner(TPlanner planner, CancellationToken cancellationToken);
    }

    internal sealed class PlannerWork<TPlanner, TParameters> : PlannerWork<TPlanner>
        where TPlanner : NavigationPlanner<TParameters>
    {
        private readonly AABB body;
        private readonly NavigationGoalRequest goal;
        private readonly TParameters parameters;
        private readonly NavigationPlanningExtent extent;

        internal PlannerWork(AABB body, NavigationGoalRequest goal, TPlanner planner, TParameters parameters, NavigationPlanningExtent extent)
            : base(planner)
        {
            this.body = body;
            this.goal = goal;
            this.parameters = parameters;
            this.extent = extent;
        }

        protected override NavigationPlanResult ExecutePlanner(TPlanner planner, CancellationToken cancellationToken)
        {
            if (extent == NavigationPlanningExtent.NextAction)
            {
                return planner.PlanSingleStep(body, goal, parameters, cancellationToken);
            }
            else
            {
                return planner.Plan(body, goal, parameters, cancellationToken);
            }
        }

    }

}
