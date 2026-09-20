using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Stable request data and expansion strategy for one action-graph search.</summary>
    public abstract class NavigationSearchRequest
    {
        public Vector2 Start { get; }
        public NavigationSupport StartSupport { get; }
        public NavigationGoalRequest Goal { get; }
        public int MaxExpandedNodes { get; }
        public int MaxTotalWorkUnits { get; }
        public NavigationNodeIdentity StartIdentity { get; }

        /// <summary>Creates stable search inputs and resolves the total work limit.</summary>
        protected NavigationSearchRequest(
            Vector2 start,
            NavigationSupport startSupport,
            NavigationGoalRequest goal,
            int maxExpandedNodes,
            NavigationNodeIdentity startIdentity,
            int maxTotalWorkUnits = -1)
        {
            Goal = goal;
            if (!NavigationNumeric.IsFinite(start))
                throw new ArgumentException("Navigation search coordinates must be finite.");
            if (maxExpandedNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
            if (maxTotalWorkUnits == 0 || maxTotalWorkUnits < -1)
                throw new ArgumentOutOfRangeException(nameof(maxTotalWorkUnits));
            Start = start;
            StartSupport = startSupport;
            MaxExpandedNodes = maxExpandedNodes;
            if (maxTotalWorkUnits > 0)
            {
                MaxTotalWorkUnits = maxTotalWorkUnits;
            }
            else
            {
                // Keep synchronous callers bounded even when every candidate needs geometry work.
                int scaledLimit = maxExpandedNodes > 256 ? int.MaxValue : maxExpandedNodes * 16;
                MaxTotalWorkUnits = Math.Min(4096, Math.Max(256, scaledLimit));
            }
            StartIdentity = startIdentity;
        }

        /// <summary>
        /// Produces resumable expansion work in candidate order. Each yielded item consumes
        /// one work unit; the search owns advancement, budget checks, and enumerator disposal.
        /// Implementations must keep request inputs stable for the search lifetime.
        /// </summary>
        public abstract IEnumerable<NavigationTransitionWork> EnumerateTransitions(NavigationSearchNode node);

        /// <summary>Estimates remaining cost; searches without a heuristic use zero.</summary>
        public virtual float EvaluateHeuristic(Vector2 position) => 0f;
    }
}
