using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns bounded LRU retention of one world's immutable support candidate queries.</summary>
    internal sealed class SupportCandidateCache
    {
        internal const int DefaultEntryLimit = 1024;
        internal const int DefaultCandidateLimit = 32768;

        private readonly object sync = new();
        private readonly int entryLimit;
        private readonly int candidateLimit;
        private readonly Dictionary<Key, Entry> entries = new();
        private readonly LinkedList<Key> lru = new();
        private int retainedCandidateCount;

        internal SupportCandidateCache(int entryLimit = DefaultEntryLimit, int candidateLimit = DefaultCandidateLimit)
        {
            if (entryLimit < 0) throw new ArgumentOutOfRangeException(nameof(entryLimit));
            if (candidateLimit < 0) throw new ArgumentOutOfRangeException(nameof(candidateLimit));
            this.entryLimit = entryLimit;
            this.candidateLimit = candidateLimit;
        }

        internal bool TryGet(AABB anchorBounds, Vector2 bodySize, out IReadOnlyList<NavigationSupportCandidate> candidates)
        {
            Key key = new(anchorBounds, bodySize);
            lock (sync)
            {
                if (!entries.TryGetValue(key, out Entry entry))
                {
                    candidates = null;
                    return false;
                }

                Touch(entry.Node);
                candidates = entry.Candidates;
                return true;
            }
        }

        /// <summary>Publishes one completed raw geometry query, reusing a concurrent publication when present.</summary>
        internal IReadOnlyList<NavigationSupportCandidate> Publish(AABB anchorBounds, Vector2 bodySize, IReadOnlyList<NavigationSupportCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (entryLimit == 0 || candidateLimit == 0 || candidates.Count > candidateLimit)
                return candidates;

            Key key = new(anchorBounds, bodySize);
            lock (sync)
            {
                if (entries.TryGetValue(key, out Entry existing))
                {
                    Touch(existing.Node);
                    return existing.Candidates;
                }

                LinkedListNode<Key> node = lru.AddFirst(key);
                entries.Add(key, new Entry(candidates, node));
                retainedCandidateCount += candidates.Count;
                Trim();
                return candidates;
            }
        }

        private void Trim()
        {
            while (entries.Count > entryLimit || retainedCandidateCount > candidateLimit)
            {
                LinkedListNode<Key> node = lru.Last;
                Entry entry = entries[node.Value];
                retainedCandidateCount -= entry.Candidates.Count;
                entries.Remove(node.Value);
                lru.RemoveLast();
            }
        }

        private void Touch(LinkedListNode<Key> node)
        {
            if (node == lru.First) return;
            lru.Remove(node);
            lru.AddFirst(node);
        }

        private readonly struct Entry
        {
            internal readonly IReadOnlyList<NavigationSupportCandidate> Candidates;
            internal readonly LinkedListNode<Key> Node;

            internal Entry(IReadOnlyList<NavigationSupportCandidate> candidates, LinkedListNode<Key> node)
            {
                Candidates = candidates;
                Node = node;
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int minX;
            private readonly int minY;
            private readonly int maxX;
            private readonly int maxY;
            private readonly int bodyX;
            private readonly int bodyY;

            internal Key(AABB bounds, Vector2 bodySize)
            {
                minX = Bits(bounds.MinX);
                minY = Bits(bounds.MinY);
                maxX = Bits(bounds.MaxX);
                maxY = Bits(bounds.MaxY);
                bodyX = Bits(bodySize.x);
                bodyY = Bits(bodySize.y);
            }

            public bool Equals(Key other)
            {
                return minX == other.minX
                    && minY == other.minY
                    && maxX == other.maxX
                    && maxY == other.maxY
                    && bodyX == other.bodyX
                    && bodyY == other.bodyY;
            }


            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(minX, minY, maxX, maxY, bodyX, bodyY);
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
