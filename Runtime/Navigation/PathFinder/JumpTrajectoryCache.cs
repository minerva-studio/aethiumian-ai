using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns bounded LRU retention of one solver's validated jump results and deterministic rejections.</summary>
    internal sealed class JumpTrajectoryCache
    {
        internal const int DefaultEntryLimit = 8192;

        private readonly object sync = new();
        private readonly int entryLimit;
        private readonly Dictionary<Key, Entry> entries = new();
        private readonly LinkedList<Key> lru = new();

        internal JumpTrajectoryCache(int entryLimit = DefaultEntryLimit)
        {
            if (entryLimit < 0) throw new ArgumentOutOfRangeException(nameof(entryLimit));
            this.entryLimit = entryLimit;
        }

        internal bool TryGet(Vector2 start, Vector2 landing, GroundJumpParameters parameters,
            out JumpTrajectorySolution trajectory)
        {
            Key key = new(start, landing, parameters);
            lock (sync)
            {
                if (!entries.TryGetValue(key, out Entry entry))
                {
                    trajectory = null;
                    return false;
                }

                Touch(entry.Node);
                trajectory = entry.Trajectory;
                return true;
            }
        }

        /// <summary>Publishes one completed result and returns the race-winning cached value.</summary>
        internal JumpTrajectorySolution Publish(Vector2 start, Vector2 landing, GroundJumpParameters parameters,
            JumpTrajectorySolution trajectory)
        {
            if (entryLimit == 0) return trajectory;

            Key key = new(start, landing, parameters);
            lock (sync)
            {
                if (entries.TryGetValue(key, out Entry existing))
                {
                    Touch(existing.Node);
                    return existing.Trajectory;
                }

                LinkedListNode<Key> node = lru.AddFirst(key);
                entries.Add(key, new Entry(trajectory, node));
                while (entries.Count > entryLimit)
                {
                    LinkedListNode<Key> oldest = lru.Last;
                    entries.Remove(oldest.Value);
                    lru.RemoveLast();
                }
                return trajectory;
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
            internal readonly JumpTrajectorySolution Trajectory;
            internal readonly LinkedListNode<Key> Node;

            internal Entry(JumpTrajectorySolution trajectory, LinkedListNode<Key> node)
            {
                Trajectory = trajectory;
                Node = node;
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int sx, sy, lx, ly, bx, by, gx, gy;
            private readonly int scale, damping, height, length, step, snap, contact;

            internal Key(Vector2 start, Vector2 landing, GroundJumpParameters parameters)
            {
                sx = Bits(start.x); sy = Bits(start.y);
                lx = Bits(landing.x); ly = Bits(landing.y);
                bx = Bits(parameters.BodySize.x); by = Bits(parameters.BodySize.y);
                gx = Bits(parameters.Gravity.x); gy = Bits(parameters.Gravity.y);
                scale = Bits(parameters.GravityScale); damping = Bits(parameters.LinearDamping);
                height = Bits(parameters.JumpHeight); length = Bits(parameters.JumpLength);
                step = Bits(parameters.SimulationTimeStep); snap = Bits(parameters.SupportSnapDistance);
                contact = Bits(parameters.GroundContactTolerance);
            }

            public bool Equals(Key other)
                => sx == other.sx && sy == other.sy && lx == other.lx && ly == other.ly
                    && bx == other.bx && by == other.by && gx == other.gx && gy == other.gy
                    && scale == other.scale && damping == other.damping && height == other.height
                    && length == other.length && step == other.step && snap == other.snap
                    && contact == other.contact;

            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode()
                => HashCode.Combine(HashCode.Combine(sx, sy, lx, ly, bx, by, gx, gy),
                    HashCode.Combine(scale, damping, height, length, step, snap, contact));
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
