using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Amlos.AI.Navigation.Util
{
    /// <summary>
    /// Generic binary min-heap with decrease-key via an index map.
    /// - Priority is int (lower = higher priority).
    /// - No per-operation allocation after growth; arrays are resized exponentially.
    /// - Not thread-safe by itself; create/use per search/thread.
    /// </summary>
    public sealed class MinHeap<TNode>
    {
        private TNode[] _nodes;
        private int[] _priorities;
        private int _count;
        private readonly Dictionary<TNode, int> _indexMap;

        public int Count => _count;

        public MinHeap(int capacity = 128, IEqualityComparer<TNode> comparer = null)
        {
            if (capacity < 1) capacity = 1;
            _nodes = new TNode[capacity];
            _priorities = new int[capacity];
            _indexMap = new Dictionary<TNode, int>(capacity, comparer ?? EqualityComparer<TNode>.Default);
        }

        /// <summary>Clears the heap while retaining allocated buffers for reuse.</summary>
        public void Clear()
        {
            _indexMap.Clear();
            _count = 0;
        }

        /// <summary>
        /// Pushes a node or decreases its priority if already present.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushOrDecrease(TNode node, int priority)
        {
            if (_indexMap.TryGetValue(node, out int i))
            {
                if (priority < _priorities[i])
                {
                    _priorities[i] = priority;
                    SiftUp(i);
                }
                return;
            }

            EnsureCapacity(_count + 1);
            int idx = _count++;
            _nodes[idx] = node;
            _priorities[idx] = priority;
            _indexMap[node] = idx;
            SiftUp(idx);
        }

        /// <summary>Pops the node with the smallest priority.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TNode Pop()
        {
            int last = --_count;
            TNode top = _nodes[0];
            _indexMap.Remove(top);

            if (last >= 0)
            {
                _nodes[0] = _nodes[last];
                _priorities[0] = _priorities[last];

                if (_count > 0)
                {
                    _indexMap[_nodes[0]] = 0;
                    SiftDown(0);
                }
            }

            return top;
        }

        /// <summary>Optionally get the current priority of a node if it exists in the heap.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPriority(TNode node, out int priority)
        {
            if (_indexMap.TryGetValue(node, out int i))
            {
                priority = _priorities[i];
                return true;
            }
            priority = 0;
            return false;
        }

        // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤ internals ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int need)
        {
            if (need <= _nodes.Length) return;
            int newCap = _nodes.Length * 2;
            if (newCap < need) newCap = need;
            Array.Resize(ref _nodes, newCap);
            Array.Resize(ref _priorities, newCap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_priorities[i] >= _priorities[p]) break;
                Swap(i, p);
                i = p;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftDown(int i)
        {
            int half = _count >> 1;
            while (i < half)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                int best = (r < _count && _priorities[r] < _priorities[l]) ? r : l;
                if (_priorities[i] <= _priorities[best]) break;
                Swap(i, best);
                i = best;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Swap(int a, int b)
        {
            (_nodes[a], _nodes[b]) = (_nodes[b], _nodes[a]);
            (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
            _indexMap[_nodes[a]] = a;
            _indexMap[_nodes[b]] = b;
        }
    }
}
