using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    /// <summary>
    /// Asset-level random source definition. Assets hold configuration; returned sources hold runtime state or wrap external state.
    /// </summary>
    public abstract class RandomSourceAsset : ScriptableObject
    {
        public virtual RandomSourceScopeMask SupportedScopes => RandomSourceScopeMask.All;

        public bool Supports(RandomSourceScope scope)
        {
            return (SupportedScopes & ToMask(scope)) != 0;
        }

        public virtual RandomSourceScope NormalizeScope(RandomSourceScope requestedScope)
        {
            if (Supports(requestedScope))
            {
                return requestedScope;
            }

            return FirstSupportedScope(SupportedScopes);
        }

        /// <summary>
        /// Creates or returns a random source for a resolver cache miss.
        /// </summary>
        /// <remarks>
        /// The returned source may be a newly-owned RNG or a wrapper over random state managed outside the resolver.
        /// </remarks>
        public abstract IRandomSource CreateSource(RandomSourceCreateContext context);

        public static RandomSourceScopeMask ToMask(RandomSourceScope scope)
        {
            return scope switch
            {
                RandomSourceScope.Entry => RandomSourceScopeMask.Entry,
                RandomSourceScope.Local => RandomSourceScopeMask.Local,
                RandomSourceScope.Static => RandomSourceScopeMask.Static,
                RandomSourceScope.Global => RandomSourceScopeMask.Global,
                _ => RandomSourceScopeMask.Global,
            };
        }

        private static RandomSourceScope FirstSupportedScope(RandomSourceScopeMask scopes)
        {
            if ((scopes & RandomSourceScopeMask.Local) != 0) return RandomSourceScope.Local;
            if ((scopes & RandomSourceScopeMask.Static) != 0) return RandomSourceScope.Static;
            if ((scopes & RandomSourceScopeMask.Global) != 0) return RandomSourceScope.Global;
            if ((scopes & RandomSourceScopeMask.Entry) != 0) return RandomSourceScope.Entry;
            return RandomSourceScope.Global;
        }
    }
}
