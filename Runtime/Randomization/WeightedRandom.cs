using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    public static class WeightedRandom
    {
        public static T Pick<T>(this IRandomSource random, IReadOnlyList<T> items, Func<T, int> getWeight)
        {
            if (items == null || items.Count == 0)
            {
                return default;
            }

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                total += Mathf.Max(0, getWeight(items[i]));
            }

            if (total <= 0)
            {
                return items[random.NextInt(items.Count)];
            }

            int roll = random.NextInt(total);
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Mathf.Max(0, getWeight(items[i]));
                if (roll < 0)
                {
                    return items[i];
                }
            }

            return items[^1];
        }
    }
}
