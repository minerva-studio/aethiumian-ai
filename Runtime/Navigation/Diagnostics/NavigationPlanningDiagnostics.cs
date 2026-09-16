using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Diagnostics;
#endif
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Collects optional, request-local planning diagnostics. It does not influence route selection,
    /// planning budgets, operation publication, or failure caching.
    /// </summary>
    public sealed class NavigationPlanningDiagnostics
    {
        /// <summary>Gets the total search expansions performed by one operation.</summary>
        public int ExpansionCount { get; private set; }

        /// <summary>Gets the total validated terminal candidates produced by one operation.</summary>
        public int TerminalCandidateCount { get; private set; }
        public int JumpCandidateGeneratedCount { get; private set; }
        public int JumpCandidatePrunedCount { get; private set; }
        public int JumpCandidateValidatedCount { get; private set; }
        public int CacheHitCount { get; private set; }
        public int CacheMissCount { get; private set; }
        public int BranchBoundCount { get; private set; }
        public int TopologyCacheHitCount { get; private set; }
        public int TopologyCacheMissCount { get; private set; }

        /// <summary>Records one search expansion.</summary>
        public void RecordExpansion() => ExpansionCount++;
        /// <summary>Records one validated terminal candidate.</summary>
        public void RecordTerminalCandidate() => TerminalCandidateCount++;
        /// <summary>Records one generated jump landing cell.</summary>
        public void RecordJumpCandidateGenerated() => JumpCandidateGeneratedCount++;
        /// <summary>Records one cheaply pruned jump landing cell.</summary>
        public void RecordJumpCandidatePruned() => JumpCandidatePrunedCount++;
        /// <summary>Records one collision-validated jump edge.</summary>
        public void RecordJumpCandidateValidated() => JumpCandidateValidatedCount++;
        /// <summary>Records one snapshot geometry cache hit.</summary>
        public void RecordCacheHit() => CacheHitCount++;
        /// <summary>Records one snapshot geometry cache miss.</summary>
        public void RecordCacheMiss() => CacheMissCount++;
        /// <summary>Records one proven open-frontier branch-bound termination.</summary>
        public void RecordBranchBound() => BranchBoundCount++;
        /// <summary>Records one reusable topology cache hit.</summary>
        public void RecordTopologyCacheHit() => TopologyCacheHitCount++;
        /// <summary>Records one reusable topology cache miss.</summary>
        public void RecordTopologyCacheMiss() => TopologyCacheMissCount++;

        public void RecordPathExpansion()
        {
            RecordExpansion();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AIPerformanceDiagnostics.RecordPathExpandedNode();
#endif
        }
    }
}
