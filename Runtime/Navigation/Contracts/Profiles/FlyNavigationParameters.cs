using System;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable approach limits used to plan aerial traversal.
    /// </summary>
    public readonly struct FlyNavigationParameters
    {
        /// <summary>
        /// Gets the remaining center-distance approach budget; zero means unlimited.
        /// </summary>
        public float RemainingApproachDistance { get; }

        /// <summary>
        /// Gets whether the remaining approach distance is an enforced finite budget.
        /// </summary>
        public bool HasApproachLimit { get; }

        /// <summary>
        /// Creates and validates immutable aerial navigation parameters.
        /// </summary>
        public FlyNavigationParameters(float remainingApproachDistance = 0f)
            : this(remainingApproachDistance, remainingApproachDistance > 0f)
        {
        }

        /// <summary>Creates aerial parameters with an explicit budget state, including an exhausted budget.</summary>
        public FlyNavigationParameters(float remainingApproachDistance, bool hasApproachLimit)
        {
            if (!NavigationNumeric.IsFinite(remainingApproachDistance) || remainingApproachDistance < 0f || (!hasApproachLimit && remainingApproachDistance != 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingApproachDistance), remainingApproachDistance, "Remaining retreat approach distance must be finite and non-negative.");
            }

            RemainingApproachDistance = remainingApproachDistance;
            HasApproachLimit = hasApproachLimit;
        }
    }
}
