using Amlos.AI.Navigation.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Amlos.AI.Navigation
{
    /// <summary>
    /// Allocation-light, thread-safe A* pathfinder for grid navigation.
    /// Uses a custom binary min-heap, stackalloc neighbor buffer, and dictionaries
    /// for gScore / cameFrom to minimize GC pressure and avoid LINQ.
    /// </summary>
    public class AStarPathFinder : PathFinder
    {
        protected const int NEIGHBOR_COST = 10;
        protected const int DIAGONAL_COST = 14;
        protected const int MAX_OPEN_TILE = 10000;
        protected const int MAX_CLOSE_TILE = 10000;
        protected float heuristicWeight = 1.5f;

        protected HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
        protected Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int>(256);
        protected Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>(256);
        protected MinHeap<Vector2Int> open = new MinHeap<Vector2Int>(256);


        public AStarPathFinder(Vector2 size, IsSolidBlock isSolidBlock, CanStandAt canStandAt)
            : base(size, isSolidBlock, canStandAt) { }

        /// <summary>
        /// Octile distance heuristic (diagonal moves allowed).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected int EstimateCost(in Vector2Int start, in Vector2Int end)
        {
            int dx = Mathf.Abs(start.x - end.x);
            int dy = Mathf.Abs(start.y - end.y);
            int min = dx < dy ? dx : dy;
            int rem = dx + dy - (min << 1);
            return (DIAGONAL_COST * min) + (NEIGHBOR_COST * rem);
        }

        /// <summary>
        /// Writes neighbors of a cell into a stackalloc buffer (max 8) and returns the count.
        /// Diagonals are skipped if they would "cut a corner" through solid blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNeighbors(in Vector2Int c, Span<Vector2Int> buf8)
        {
            int n = 0;
            // 4-neighborhood
            buf8[n++] = new Vector2Int(c.x + 1, c.y);
            buf8[n++] = new Vector2Int(c.x - 1, c.y);
            buf8[n++] = new Vector2Int(c.x, c.y + 1);
            buf8[n++] = new Vector2Int(c.x, c.y - 1);

            // diagonals (avoid corner cutting)
            if (!IsCorner(c, 1, 1)) buf8[n++] = new Vector2Int(c.x + 1, c.y + 1);
            if (!IsCorner(c, 1, -1)) buf8[n++] = new Vector2Int(c.x + 1, c.y - 1);
            if (!IsCorner(c, -1, 1)) buf8[n++] = new Vector2Int(c.x - 1, c.y + 1);
            if (!IsCorner(c, -1, -1)) buf8[n++] = new Vector2Int(c.x - 1, c.y - 1);

            return n;
        }

        protected bool CanStandAt(Vector2Int dest, bool needFoothold = false)
            => CanStandAt(new Vector3(dest.x, dest.y, 0), needFoothold);

        protected bool CanStandAt(Vector3 dest, bool needFoothold = false)
            => canStandAt?.Invoke(dest, size, needFoothold) == true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsSolidBlock(Vector2Int p) => isSolidBlock?.Invoke(p) != false;

        /// <summary>
        /// Corner-cutting check consistent with the original implementation:
        /// a diagonal move is blocked if both orthogonal side cells are solid.
        /// </summary>
        protected bool IsCorner(Vector2Int c, int ox, int oy)
        {
            return IsSolidBlock(new Vector2Int(c.x + ox, c.y + oy - (oy >= 0 ? 1 : -1)))
                && IsSolidBlock(new Vector2Int(c.x + ox - (ox >= 0 ? 1 : -1), c.y + oy));
        }

        /// <summary>
        /// Allocation-light, thread-safe A*.
        /// All per-search state is local, so multiple threads can call this concurrently
        /// as long as isSolidBlock/canStandAt are themselves thread-safe.
        /// </summary>
        public override List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (start == goal) return new List<Vector2Int> { goal };
            if (IsSolidBlock(goal) || !CanStandAt(goal)) return null;

            closed.Clear();
            gScore.Clear();
            cameFrom.Clear();
            open.Clear();

            gScore[start] = 0;
            open.PushOrDecrease(start, (int)(heuristicWeight * EstimateCost(start, goal)));

            // Stack-allocated neighbor buffer (exactly 8 slots)
            Span<Vector2Int> nbr = stackalloc Vector2Int[8];

            while (open.Count > 0)
            {
                if (open.Count > MAX_OPEN_TILE) return null;   // safety bailout
                if (closed.Count > MAX_CLOSE_TILE) return null; // safety bailout

                var current = open.Pop();
                if (current == goal)
                    return ReconstructPath(current);

                if (!closed.Add(current)) continue;

                int nbrCount = GetNeighbors(current, nbr);
                for (int i = 0; i < nbrCount; i++)
                {
                    var nb = nbr[i];

                    if (closed.Contains(nb)) continue;

                    // Solid or not standable cells are immediately discarded and marked closed.
                    if (IsSolidBlock(nb) || !CanStandAt(nb))
                    {
                        closed.Add(nb);
                        continue;
                    }

                    // Edge cost: straight vs diagonal
                    int step = (nb.x == current.x || nb.y == current.y) ? NEIGHBOR_COST : DIAGONAL_COST;

                    int gCurr = gScore.TryGetValue(current, out var gC) ? gC : int.MaxValue;
                    int tentative = gCurr + step;

                    int gOld = gScore.TryGetValue(nb, out var gN) ? gN : int.MaxValue;
                    if (tentative >= gOld) continue;

                    cameFrom[nb] = current;
                    gScore[nb] = tentative;

                    int f = tentative + (int)(heuristicWeight * EstimateCost(nb, goal));
                    open.PushOrDecrease(nb, f);
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Reconstructs the path by walking backward through the cameFrom map.
        /// </summary>
        protected List<Vector2Int> ReconstructPath(Vector2Int last)
        {
            var path = new List<Vector2Int>(Mathf.Max(4, cameFrom.Count + 1));
            path.Add(last);
            while (cameFrom.TryGetValue(last, out var prev))
            {
                last = prev;
                path.Add(last);
            }
            path.Reverse();
            return path;
        }
    }
}
