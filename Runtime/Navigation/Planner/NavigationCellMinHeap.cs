using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Deterministic binary min-heap for aerial cell searches.</summary>
    internal sealed class NavigationCellMinHeap
    {
        private readonly List<Entry> entries = new();

        /// <summary>Gets whether the heap contains no cells.</summary>
        public bool IsEmpty => entries.Count == 0;

        /// <summary>Adds one cell ordered by score and heuristic.</summary>
        public void Enqueue(Vector2Int cell, float score, float heuristic)
            => Enqueue(cell, score, heuristic, 0);

        /// <summary>Adds one labelled cell while preserving deterministic ordering for equal scores.</summary>
        public void Enqueue(Vector2Int cell, float score, float heuristic, int labelId)
        {
            entries.Add(new Entry(cell, score, heuristic, labelId));
            int index = entries.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsBefore(entries[index], entries[parent])) break;
                (entries[index], entries[parent]) = (entries[parent], entries[index]);
                index = parent;
            }
        }

        /// <summary>Removes the lowest-score cell.</summary>
        public Vector2Int Dequeue(out float score)
            => Dequeue(out score, out _);

        /// <summary>Removes the lowest-score cell and returns its search-label identity.</summary>
        public Vector2Int Dequeue(out float score, out int labelId)
        {
            if (entries.Count == 0) throw new InvalidOperationException("The navigation open set is empty.");
            Vector2Int result = entries[0].Cell;
            labelId = entries[0].LabelId;
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

        private static bool IsBefore(Entry left, Entry right)
        {
            if (left.Score < right.Score - NavigationTolerances.Epsilon) return true;
            if (Mathf.Abs(left.Score - right.Score) > NavigationTolerances.Epsilon) return false;
            if (left.Heuristic < right.Heuristic - NavigationTolerances.Epsilon) return true;
            if (Mathf.Abs(left.Heuristic - right.Heuristic) > NavigationTolerances.Epsilon) return false;
            int y = left.Cell.y.CompareTo(right.Cell.y);
            if (y != 0) return y < 0;
            int x = left.Cell.x.CompareTo(right.Cell.x);
            return x != 0 ? x < 0 : left.LabelId < right.LabelId;
        }

        private readonly struct Entry
        {
            public readonly Vector2Int Cell;
            public readonly float Score;
            public readonly float Heuristic;
            public readonly int LabelId;

            public Entry(Vector2Int cell, float score, float heuristic, int labelId)
            {
                Cell = cell;
                Score = score;
                Heuristic = heuristic;
                LabelId = labelId;
            }
        }
    }
}
