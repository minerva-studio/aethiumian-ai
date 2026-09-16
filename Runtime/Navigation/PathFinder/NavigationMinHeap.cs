using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Deterministic binary min-heap used by incremental navigation searches.</summary>
    internal sealed class NavigationMinHeap<T>
    {
        private readonly IComparer<T> keyComparer;
        private readonly List<Entry> entries = new();

        /// <summary>Creates a deterministic score heap with an explicit key tie-breaker.</summary>
        public NavigationMinHeap(IComparer<T> keyComparer)
        {
            this.keyComparer = keyComparer ?? throw new ArgumentNullException(nameof(keyComparer));
        }

        /// <summary>Gets whether the heap contains no entries.</summary>
        public bool IsEmpty => entries.Count == 0;

        /// <summary>Gets the current minimum score without removing its entry.</summary>
        public bool TryPeekScore(out float score)
        {
            score = entries.Count == 0 ? 0f : entries[0].Score;
            return entries.Count != 0;
        }

        /// <summary>Adds one entry while preserving the minimum-score ordering.</summary>
        public void Enqueue(T key, float score, float heuristic)
        {
            Entry entry = new(key, score, heuristic);
            entries.Add(entry);
            int index = entries.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsBefore(entries[index], entries[parent])) break;
                (entries[index], entries[parent]) = (entries[parent], entries[index]);
                index = parent;
            }
        }

        /// <summary>Removes the lowest-score entry.</summary>
        public T Dequeue(out float score)
        {
            if (entries.Count == 0) throw new InvalidOperationException("The navigation open set is empty.");
            T result = entries[0].Key;
            score = entries[0].Score;
            int last = entries.Count - 1;
            entries[0] = entries[last];
            entries.RemoveAt(last);
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= entries.Count) break;
                int right = left + 1;
                int child = right < entries.Count && IsBefore(entries[right], entries[left]) ? right : left;
                if (!IsBefore(entries[child], entries[index])) break;
                (entries[index], entries[child]) = (entries[child], entries[index]);
                index = child;
            }

            return result;
        }

        private bool IsBefore(Entry left, Entry right)
        {
            if (left.Score < right.Score - NavigationTolerances.Epsilon) return true;
            if (Mathf.Abs(left.Score - right.Score) > NavigationTolerances.Epsilon) return false;
            if (left.Heuristic < right.Heuristic - NavigationTolerances.Epsilon) return true;
            if (Mathf.Abs(left.Heuristic - right.Heuristic) > NavigationTolerances.Epsilon) return false;
            return keyComparer.Compare(left.Key, right.Key) < 0;
        }

        private readonly struct Entry
        {
            public readonly T Key;
            public readonly float Score;
            public readonly float Heuristic;

            public Entry(T key, float score, float heuristic)
            {
                Key = key;
                Score = score;
                Heuristic = heuristic;
            }
        }
    }
}
