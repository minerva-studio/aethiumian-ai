using System;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Identifies the terminal planning condition without attempting to classify every failure cause.</summary>
    public enum NavigationPlanTermination
    {
        NoResult,
        ResultProduced,
        SearchExhausted,
        BudgetReached,
    }

    /// <summary>Immutable route and terminal-condition data produced by one navigation request.</summary>
    public readonly struct NavigationPlanResult
    {
        /// <summary>Gets an ordinary early exit that does not prove search exhaustion.</summary>
        public static NavigationPlanResult NoResult => default;



        /// <summary>Gets the selected route, when planning produced one.</summary>
        public NavigationRoute Route { get; }

        /// <summary>Gets why planning reached a terminal result.</summary>
        public NavigationPlanTermination Termination { get; }

        private NavigationPlanResult(NavigationRoute route, NavigationPlanTermination termination)
        {
            if (termination != NavigationPlanTermination.ResultProduced && route.HasValue)
                throw new ArgumentException("Only a produced result may carry a route.", nameof(route));
            Route = route;
            Termination = termination;
        }



        /// <summary>Creates a result that produced an executable route.</summary>
        public static NavigationPlanResult ResultProduced(NavigationRoute route)
        {
            if (!route.HasValue) throw new ArgumentNullException(nameof(route));
            return new NavigationPlanResult(route, NavigationPlanTermination.ResultProduced);
        }

        /// <summary>Creates a result after the search frontier was exhausted.</summary>
        public static NavigationPlanResult SearchExhausted()
            => new(default, NavigationPlanTermination.SearchExhausted);

        /// <summary>Creates a result after the request's total search budget was exhausted.</summary>
        public static NavigationPlanResult BudgetReached()
            => new(default, NavigationPlanTermination.BudgetReached);

        /// <summary>Replaces the route while preserving this result's terminal condition.</summary>
        public NavigationPlanResult WithRoute(NavigationRoute route)
        {
            if (!route.HasValue) throw new ArgumentNullException(nameof(route));
            return Termination switch
            {
                NavigationPlanTermination.ResultProduced => ResultProduced(route),
                _ => throw new InvalidOperationException(
                    "A non-produced planning outcome cannot be assigned a route."),
            };
        }
    }
}
