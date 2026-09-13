using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Immutable geometry inputs used to plan aerial traversal.</summary>
    public readonly struct FlyNavigationParameters
    {
        /// <summary>Gets the agent body size in world units.</summary>
        public Vector2 BodySize { get; }
        /// <summary>Gets the remaining center-distance approach budget; zero means unlimited.</summary>
        public float RemainingApproachDistance { get; }
        /// <summary>Gets whether the remaining approach distance is an enforced finite budget.</summary>
        public bool HasApproachLimit { get; }

        /// <summary>Creates and validates immutable aerial navigation parameters.</summary>
        public FlyNavigationParameters(Vector2 bodySize, float remainingApproachDistance = 0f)
            : this(bodySize, remainingApproachDistance, remainingApproachDistance > 0f)
        {
        }

        /// <summary>Creates aerial parameters with an explicit budget state, including an exhausted budget.</summary>
        public FlyNavigationParameters(Vector2 bodySize, float remainingApproachDistance, bool hasApproachLimit)
        {
            if (!NavigationNumeric.IsFinite(bodySize) || bodySize.x <= 0 || bodySize.y <= 0)
            {
                throw new ArgumentException("Body size must be finite and positive.", nameof(bodySize));
            }
            if (!NavigationNumeric.IsFinite(remainingApproachDistance) || remainingApproachDistance < 0f || (!hasApproachLimit && remainingApproachDistance != 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingApproachDistance), remainingApproachDistance, "Remaining retreat approach distance must be finite and non-negative.");
            }

            BodySize = bodySize;
            RemainingApproachDistance = remainingApproachDistance;
            HasApproachLimit = hasApproachLimit;
        }

    }
}
