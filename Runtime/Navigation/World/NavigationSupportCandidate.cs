using System;

namespace Aethiumian.AI.Navigation
{
    /// <summary>One immutable geometry candidate owned by a navigation snapshot.</summary>
    public readonly struct NavigationSupportCandidate
    {
        /// <summary>Gets the candidate identity, unique only within its owning snapshot.</summary>
        public int Id { get; }

        /// <summary>Gets the physical support represented by this search candidate.</summary>
        public NavigationSupport Support { get; }

        /// <summary>Creates a snapshot-owned support candidate.</summary>
        public NavigationSupportCandidate(int id, NavigationSupport support)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
            Support = support;
        }
    }
}
