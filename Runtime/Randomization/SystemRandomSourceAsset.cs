using System;
using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    [CreateAssetMenu(fileName = "AI_Random_System", menuName = "Aethiumian AI/Random/System")]
    public class SystemRandomSourceAsset : RandomSourceAsset
    {
        public enum SeedMode
        {
            FixedSeed,
            RandomOnCreate,
        }

        public SeedMode seedMode = SeedMode.FixedSeed;
        public int seed;

        public override RandomSourceScopeMask SupportedScopes => RandomSourceScopeMask.All;

        public override IRandomSource CreateSource(RandomSourceCreateContext request)
        {
            int combinedSeed = CreateSeed(request.SeedSalt);
            return new SystemRandomSource(combinedSeed);
        }

        private int CreateSeed(int seedSalt)
        {
            if (seedMode == SeedMode.RandomOnCreate)
            {
                // RandomOnCreate is intentionally non-deterministic only when the resolver creates a new source.
                return unchecked((int)DateTime.UtcNow.Ticks ^ Guid.NewGuid().GetHashCode() ^ seedSalt);
            }

            return unchecked(seed * 397 ^ seedSalt);
        }
    }

    public class SystemRandomSource : IRandomSource
    {
        private readonly System.Random random;

        public SystemRandomSource(int seed)
        {
            random = new System.Random(seed);
        }

        public int NextInt(int maxExclusive)
        {
            return random.Next(maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return (float)random.NextDouble();
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return (float)(random.NextDouble() * (maxExclusive - minInclusive) + minInclusive);
        }
    }
}
