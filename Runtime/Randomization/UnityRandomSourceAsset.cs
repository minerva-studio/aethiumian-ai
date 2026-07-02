using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    /// <summary>
    /// Optional asset wrapper for explicitly selecting Unity random in AI settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AI_Random_Unity", menuName = "Aethiumian AI/Random/Unity")]
    public sealed class UnityRandomSourceAsset : RandomSourceAsset
    {
        public override RandomSourceScopeMask SupportedScopes => RandomSourceScopeMask.Global;

        public override IRandomSource CreateSource(RandomSourceCreateContext context)
        {
            return UnityRandomSource.Shared;
        }
    }

    /// <summary>
    /// Unity-backed fallback source used when no configured source is available.
    /// </summary>
    public sealed class UnityRandomSource : IRandomSource
    {
        public static readonly UnityRandomSource Shared = new();

        private UnityRandomSource()
        {
        }

        public int NextInt(int maxExclusive)
        {
            return Random.Range(0, maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return Random.Range(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return Random.value;
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return Random.Range(minInclusive, maxExclusive);
        }
    }
}
