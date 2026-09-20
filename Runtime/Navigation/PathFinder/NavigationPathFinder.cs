using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    internal enum NavigationSearchStatus
    {
        Pending,
        CompleteRoute,
        Exhausted,
        BudgetReached,
    }

    /// <summary>Limits one cooperative search advance by action work and elapsed wall time.</summary>
    public readonly struct NavigationWorkBudget
    {
        public int MaxWorkUnits { get; }
        public double Milliseconds { get; }

        public NavigationWorkBudget(int maxWorkUnits = 64, double milliseconds = 2d)
        {
            if (maxWorkUnits <= 0) throw new ArgumentOutOfRangeException(nameof(maxWorkUnits));
            if (!NavigationNumeric.IsFinite(milliseconds) || milliseconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            MaxWorkUnits = maxWorkUnits;
            Milliseconds = milliseconds;
        }
    }

    /// <summary>Distinguishes support-candidate nodes from flight-lattice nodes.</summary>
    public enum NavigationNodeKind
    {
        Ground,
        Fly,
    }

    /// <summary>Identifies a search state independently of the action used to reach it.</summary>
    public readonly struct NavigationNodeIdentity : IEquatable<NavigationNodeIdentity>, IComparable<NavigationNodeIdentity>
    {
        /// <summary>Gets the state category, independently of planner mode or incoming action.</summary>
        public NavigationNodeKind Kind { get; }

        /// <summary>
        /// Gets the fly planner's own search-lattice coordinate; unused by grounded nodes.
        /// </summary>
        public Vector2Int Cell { get; }

        /// <summary>
        /// Gets the stable support-candidate identity shared by Walk and Jump searches.
        /// </summary>
        public int CandidateId { get; }

        /// <summary>
        /// Gets the label ID for the node.
        /// </summary>
        public int LabelId { get; }

        private NavigationNodeIdentity(NavigationNodeKind kind, int candidateId, Vector2Int cell, int labelId)
        {
            Kind = kind;
            CandidateId = candidateId;
            Cell = cell;
            LabelId = labelId;
        }

        public static NavigationNodeIdentity Ground(int candidateId)
            => new(NavigationNodeKind.Ground, candidateId, default, 0);

        public static NavigationNodeIdentity Fly(Vector2Int cell, int labelId = 0)
            => new(NavigationNodeKind.Fly, -1, cell, labelId);

        public bool Equals(NavigationNodeIdentity other)
            => Kind == other.Kind && CandidateId == other.CandidateId && Cell == other.Cell && LabelId == other.LabelId;

        public override bool Equals(object obj) => obj is NavigationNodeIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, CandidateId, Cell, LabelId);

        public int CompareTo(NavigationNodeIdentity other)
        {
            int kind = ((int)Kind).CompareTo((int)other.Kind);
            if (kind != 0) return kind;
            int candidate = CandidateId.CompareTo(other.CandidateId);
            if (candidate != 0) return candidate;
            int y = Cell.y.CompareTo(other.Cell.y);
            if (y != 0) return y;
            int x = Cell.x.CompareTo(other.Cell.x);
            if (x != 0) return x;
            return LabelId.CompareTo(other.LabelId);
        }
    }

    /// <summary>Read-only state supplied to an action generator for one expanded search node.</summary>
    public sealed class NavigationSearchNode
    {
        internal NavigationSearchNode(NavigationNodeIdentity identity, Vector2 position, NavigationSupport support, float heuristic)
        {
            Identity = identity;
            Position = position;
            Support = support;
            Heuristic = heuristic;
        }

        public NavigationNodeIdentity Identity { get; }
        public Vector2 Position { get; }
        public NavigationSupport Support { get; }
        public float Heuristic { get; internal set; }
        internal bool IsClosed { get; set; }
        internal NavigationSearch.PathRecord BestPath { get; set; }
    }

    /// <summary>One physically validated action edge in the shared navigation graph.</summary>
    public readonly struct NavigationTransition
    {
        public NavigationNodeIdentity Destination { get; }
        public Vector2 DestinationPosition { get; }
        public NavigationSupport DestinationSupport { get; }
        public NavigationRouteSegment Segment { get; }
        public float Cost { get; }
        public bool CompletesGoal { get; }

        private NavigationTransition(NavigationNodeIdentity destination, Vector2 destinationPosition, NavigationSupport destinationSupport, NavigationRouteSegment segment, float cost, bool completesGoal)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (cost < 0f || !NavigationNumeric.IsFinite(cost))
                throw new ArgumentOutOfRangeException(nameof(cost));
            Destination = destination;
            DestinationPosition = destinationPosition;
            DestinationSupport = destinationSupport;
            Segment = segment;
            Cost = cost;
            CompletesGoal = completesGoal;
        }

        public static NavigationTransition GroundSuccessor(int candidateId, Vector2 position, NavigationSupport support, NavigationRouteSegment segment, float cost, bool completesGoal)
            => new(NavigationNodeIdentity.Ground(candidateId), position, support, segment, cost, completesGoal);

        public static NavigationTransition CompletedJump(Vector2 position, NavigationRouteSegment segment, float cost)
            => new(NavigationNodeIdentity.Ground(-1), position, default, segment, cost, true);

        public static NavigationTransition JumpLanding(NavigationNodeIdentity destination, Vector2 position, NavigationSupport support, NavigationRouteSegment segment, float cost, bool completesGoal)
            => new(destination, position, support, segment, cost, completesGoal);

        public static NavigationTransition FlyMove(Vector2Int destinationCell, Vector2 position, NavigationRouteSegment segment, float cost, bool completesGoal)
            => new(NavigationNodeIdentity.Fly(destinationCell), position, default, segment, cost, completesGoal);
    }

    /// <summary>One resumable unit from an action generator; empty units account for geometry work.</summary>
    public readonly struct NavigationTransitionWork
    {
        public NavigationTransition Transition { get; }
        public bool HasTransition { get; }

        private NavigationTransitionWork(NavigationTransition transition, bool hasTransition)
        {
            Transition = transition;
            HasTransition = hasTransition;
        }

        public static NavigationTransitionWork WorkUnit => default;

        public static NavigationTransitionWork Edge(NavigationTransition transition)
            => new(transition, true);
    }

    internal readonly struct NavigationSearchUpdate
    {
        public NavigationSearchStatus Status { get; }
        public NavigationRoute Route { get; }
        public int WorkUnits { get; }
        public int ExpandedNodes { get; }

        private NavigationSearchUpdate(NavigationSearchStatus status, NavigationRoute route, int workUnits, int expandedNodes)
        {
            Status = status;
            Route = route;
            WorkUnits = workUnits;
            ExpandedNodes = expandedNodes;
        }

        public static NavigationSearchUpdate Pending(int expandedNodes)
            => new(NavigationSearchStatus.Pending, null, 0, expandedNodes);

        public static NavigationSearchUpdate CompletedRoute(NavigationRoute route, int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.CompleteRoute, route, workUnits, expandedNodes);

        public static NavigationSearchUpdate Exhausted(int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.Exhausted, null, workUnits, expandedNodes);

        public static NavigationSearchUpdate BudgetReached(int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.BudgetReached, null, workUnits, expandedNodes);
    }

    /// <summary>Cooperatively advances one action-graph search without owning Unity objects.</summary>
    internal sealed class NavigationSearch : IDisposable
    {
        private const float Tolerance = NavigationConstant.Epsilon;
        private readonly NavigationSearchRequest request;
        private readonly Dictionary<NavigationNodeIdentity, NavigationSearchNode> nodes = new();
        private readonly NavigationMinHeap<NavigationNodeIdentity> open = new();
        private NavigationSearchNode expandingNode;
        private IEnumerator<NavigationTransitionWork> transitionEnumerator;
        private bool terminal;
        private int expandedNodes;
        private int totalWorkUnits;
        private int disposed;

        internal NavigationSearch(NavigationSearchRequest request)
        {
            this.request = request;
            float heuristic = SafeHeuristic(request.Start);
            NavigationSearchNode start = new(request.StartIdentity, request.Start, request.StartSupport, heuristic)
            {
                BestPath = new PathRecord(null, null, 0f, 0),
            };
            nodes.Add(start.Identity, start);
            open.Enqueue(start.Identity, heuristic, heuristic);
        }

        /// <summary>
        /// Advances the search by a bounded slice. A terminal search returns Pending on
        /// subsequent calls and must be discarded by its owner.
        /// </summary>
        public NavigationSearchUpdate Advance(NavigationWorkBudget budget, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (budget.MaxWorkUnits <= 0)
                throw new ArgumentOutOfRangeException(nameof(budget), "Navigation work budget must allow at least one work unit.");
            if (!NavigationNumeric.IsFinite(budget.Milliseconds)
                || budget.Milliseconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(budget), "Navigation work budget must have a finite positive time slice.");
            if (terminal)
                return NavigationSearchUpdate.Pending(expandedNodes);

            long startedAt = Stopwatch.GetTimestamp();
            int workUnits = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (WorkLimitReached(startedAt, workUnits, budget))
                    return CreateBudgetUpdate(workUnits, false);

                if (transitionEnumerator == null)
                {
                    if (expandedNodes >= request.MaxExpandedNodes)
                    {
                        if (!HasOpenCandidate())
                        {
                            terminal = true;
                            return NavigationSearchUpdate.Exhausted(workUnits, expandedNodes);
                        }

                        return CreateBudgetUpdate(workUnits, true);
                    }

                    if (!TryTakeNext(out expandingNode))
                    {
                        terminal = true;
                        return NavigationSearchUpdate.Exhausted(workUnits, expandedNodes);
                    }

                    expandingNode.IsClosed = true;
                    expandedNodes++;
                    transitionEnumerator = request.EnumerateTransitions(expandingNode)?.GetEnumerator();
                    if (transitionEnumerator == null) continue;
                }

                if (!transitionEnumerator.MoveNext())
                {
                    transitionEnumerator.Dispose();
                    transitionEnumerator = null;
                    expandingNode = null;
                    continue;
                }

                workUnits++;
                totalWorkUnits++;
                NavigationTransitionWork transitionWork = transitionEnumerator.Current;
                if (!transitionWork.HasTransition)
                {
                    if (totalWorkUnits >= request.MaxTotalWorkUnits)
                        return CreateBudgetUpdate(workUnits, true);
                    if (WorkLimitReached(startedAt, workUnits, budget))
                        return CreateBudgetUpdate(workUnits, false);
                    continue;
                }

                NavigationTransition transition = transitionWork.Transition;
                ValidateTransitionResult(transition);
                if (transition.CompletesGoal)
                {
                    terminal = true;
                    return NavigationSearchUpdate.CompletedRoute(BuildRoute(expandingNode, transition), workUnits, expandedNodes);
                }

                Relax(expandingNode, transition);
                if (totalWorkUnits >= request.MaxTotalWorkUnits)
                    return CreateBudgetUpdate(workUnits, true);
                if (WorkLimitReached(startedAt, workUnits, budget))
                    return CreateBudgetUpdate(workUnits, false);
            }

            return CreateBudgetUpdate(workUnits, true);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            transitionEnumerator?.Dispose();
            transitionEnumerator = null;
            nodes.Clear();
        }

        private NavigationSearchUpdate CreateBudgetUpdate(int workUnits, bool totalBudgetReached)
        {
            if (!totalBudgetReached) return NavigationSearchUpdate.Pending(expandedNodes);
            terminal = true;
            return NavigationSearchUpdate.BudgetReached(workUnits, expandedNodes);
        }

        private void Relax(NavigationSearchNode parent, NavigationTransition transition)
        {
            float heuristic = SafeHeuristic(transition.DestinationPosition);
            if (!nodes.TryGetValue(transition.Destination, out NavigationSearchNode destination))
            {
                destination = new NavigationSearchNode(transition.Destination, transition.DestinationPosition, transition.DestinationSupport, heuristic);
                nodes.Add(destination.Identity, destination);
            }

            float routeCost = parent.BestPath.RouteCost + transition.Cost;
            int stepCount = parent.BestPath.StepCount + 1;
            if (destination.BestPath != null && !IsBetterPath(routeCost, stepCount, destination.BestPath)) return;

            PathRecord candidatePath = new(parent.BestPath, transition.Segment, routeCost, stepCount);

            destination.Heuristic = heuristic;
            destination.BestPath = candidatePath;
            destination.IsClosed = false;
            open.Enqueue(destination.Identity, candidatePath.RouteCost + heuristic, heuristic);

        }

        private bool TryTakeNext(out NavigationSearchNode selected)
        {
            while (!open.IsEmpty)
            {
                NavigationNodeIdentity identity = open.Dequeue(out float queuedScore);
                selected = nodes[identity];
                if (selected.IsClosed || selected.BestPath == null) continue;
                float expected = selected.BestPath.RouteCost + selected.Heuristic;
                if (Mathf.Abs(queuedScore - expected) > Tolerance) continue;
                return true;
            }

            selected = null;
            return false;
        }

        private bool HasOpenCandidate()
        {
            if (!TryTakeNext(out NavigationSearchNode selected)) return false;
            open.Enqueue(selected.Identity, selected.BestPath.RouteCost + selected.Heuristic, selected.Heuristic);
            return true;
        }

        private NavigationRoute BuildRoute(NavigationSearchNode node, NavigationTransition terminalTransition)
        {
            List<NavigationRouteSegment> reversed = new();
            reversed.Add(terminalTransition.Segment);

            for (PathRecord path = node.BestPath; path != null && path.Step != null; path = path.Parent)
            {
                reversed.Add(path.Step);
            }

            reversed.Reverse();
            // The route derives its own Start from the chain; this search owns the invariant that
            // the chain begins at the origin it was asked to plan from.
            if (!reversed[0].Start.Equals(request.Start))
                throw new InvalidOperationException("A navigation search route must begin at the request origin.");
            return NavigationRoute.Complete(request.Goal, reversed);
        }

        private static void ValidateTransitionResult(NavigationTransition transition)
        {
            // Segment ownership and route-chain continuity are validated by the
            // transition/route constructors. Search only rejects invalid destinations.
            if (!NavigationNumeric.IsFinite(transition.DestinationPosition))
                throw new InvalidOperationException("Navigation transition destination must be finite.");
        }

        private float SafeHeuristic(Vector2 position)
        {
            float heuristic = request.EvaluateHeuristic(position);
            return !NavigationNumeric.IsFinite(heuristic) || heuristic < 0f ? 0f : heuristic;
        }

        private static bool IsBetterPath(float routeCost, int stepCount, PathRecord best)
            => routeCost < best.RouteCost - Tolerance || (Mathf.Abs(routeCost - best.RouteCost) <= Tolerance && stepCount < best.StepCount);

        private static bool WorkLimitReached(long startedAt, int workUnits, NavigationWorkBudget budget)
            => workUnits >= budget.MaxWorkUnits || (Stopwatch.GetTimestamp() - startedAt) * (1000d / Stopwatch.Frequency) >= budget.Milliseconds;


        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(NavigationSearch));
        }

        internal sealed class PathRecord
        {
            public readonly PathRecord Parent;
            public readonly NavigationRouteSegment Step;
            public readonly float RouteCost;
            public readonly int StepCount;
            public PathRecord(PathRecord parent, NavigationRouteSegment step, float routeCost, int stepCount)
            {
                Parent = parent;
                Step = step;
                RouteCost = routeCost;
                StepCount = stepCount;
            }
        }
    }
}
