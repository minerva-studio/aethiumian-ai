using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Implements support-candidate queries over immutable geometry supplied by a subclass.
    /// This world owns only its bounded derived geometry cache; trajectory solving is a project-level concern.
    /// </summary>
    public abstract class NavigationWorld : INavigationWorld
    {
        private readonly SupportCandidateCache supportCandidateCache;

        /// <summary>Creates a world with the standard bounded support-candidate cache.</summary>
        protected NavigationWorld() : this(SupportCandidateCache.DefaultEntryLimit, SupportCandidateCache.DefaultCandidateLimit)
        {
        }

        /// <summary>Creates a package-local world with explicit support cache limits for focused validation.</summary>
        private protected NavigationWorld(int supportCacheEntryLimit, int supportCacheCandidateLimit)
        {
            supportCandidateCache = new SupportCandidateCache(supportCacheEntryLimit, supportCacheCandidateLimit);
        }

        /// <inheritdoc />
        public abstract AABB WorldBounds { get; }

        /// <inheritdoc />
        public abstract bool IsBodyClear(AABB body, float surfaceContactTolerance);

        /// <inheritdoc />
        public abstract bool IsBodyPathClear(AABB startBody, Vector2 displacement, float surfaceContactTolerance);

        /// <inheritdoc />
        public abstract bool IsLineOfSightClear(Vector2 start, Vector2 end);

        /// <inheritdoc />
        public abstract bool TryResolveSupport(AABB body, float snapDistance, out NavigationSupport support);

        /// <inheritdoc />
        public abstract bool TryGetSupportBelow(Vector2 position, out NavigationSupport support);

        /// <inheritdoc />
        public abstract void CollectOneWayCrossings(AABB previousBody, Vector2 displacement, List<NavigationSurfaceCrossing> results);

        /// <inheritdoc />
        public abstract bool AreInSameRegion(Vector2 first, Vector2 second);

        /// <summary>Gets a stable read-only candidate set for one exact bounds and body-size query.</summary>
        public IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(AABB anchorBounds, Vector2 bodySize)
        {
            if (supportCandidateCache.TryGet(anchorBounds, bodySize, out IReadOnlyList<NavigationSupportCandidate> cached))
                return cached;

            List<NavigationSupportCandidate> collected = new();
            CollectSupportCandidatesCore(anchorBounds, bodySize, collected);
            collected.Sort((left, right) => left.Id.CompareTo(right.Id));
            return supportCandidateCache.Publish(anchorBounds, bodySize, collected);
        }

        /// <summary>Appends support candidates to the caller-owned collection without clearing it.</summary>
        public void CollectSupportCandidates(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            IReadOnlyList<NavigationSupportCandidate> candidates = GetSupportCandidates(anchorBounds, bodySize);
            for (int index = 0; index < candidates.Count; index++) results.Add(candidates[index]);
        }

        /// <summary>
        /// Fills the supplied request-local list from immutable geometry without reentering the public query.
        /// Calls may overlap. Implementations must not retain the list for later mutation.
        /// This base class sorts and publishes it; nobody may mutate or reuse it after publication.
        /// </summary>
        protected abstract void CollectSupportCandidatesCore(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results);
    }
}
