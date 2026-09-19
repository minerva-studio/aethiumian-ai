using System;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable approach limits used to plan aerial traversal.
    /// </summary>
    public readonly struct FlyNavigationParameters
    {
        /// <summary>
        /// Gets the remaining center-distance approach budget, or null when there is no finite limit.
        /// A finite zero means the budget is exhausted.
        /// </summary>
        public float? RemainingApproachDistance { get; }

        /// <summary>
        /// Creates and validates immutable aerial navigation parameters. Null means unlimited;
        /// a finite zero means the approach budget is exhausted.
        /// </summary>
        public FlyNavigationParameters(float? remainingApproachDistance = null)
        {
            if (remainingApproachDistance.HasValue)
                Validate.NonNegativeFinite(remainingApproachDistance.Value, nameof(remainingApproachDistance));

            RemainingApproachDistance = remainingApproachDistance;
        }
    }
}
