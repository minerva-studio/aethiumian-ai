using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    [Flags]
    public enum NavigationActions
    {
        GroundMove = 1,
        Jump = 2,
        Fall = 4,
        DropThrough = 8,
        Fly = 16,
    }

    public enum NavigationSearchStatus
    {
        Pending,
        ExecutablePrefix,
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

    public readonly struct NavigationNodeIdentity : IEquatable<NavigationNodeIdentity>
    {
        public NavigationActions Action { get; }
        public int CandidateId { get; }
        public Vector2Int Cell { get; }
        public int LabelId { get; }

        private NavigationNodeIdentity(NavigationActions action, int candidateId, Vector2Int cell, int labelId)
        {
            Action = action;
            CandidateId = candidateId;
            Cell = cell;
            LabelId = labelId;
        }

        public static NavigationNodeIdentity Ground(int candidateId)
            => new(NavigationActions.GroundMove, candidateId, default, 0);

        public static NavigationNodeIdentity Jump(int candidateId)
            => new(NavigationActions.Jump, candidateId, default, 0);

        public static NavigationNodeIdentity Fly(Vector2Int cell, int labelId = 0)
            => new(NavigationActions.Fly, -1, cell, labelId);

        public bool Equals(NavigationNodeIdentity other)
            => Action == other.Action && CandidateId == other.CandidateId && Cell == other.Cell && LabelId == other.LabelId;

        public override bool Equals(object obj) => obj is NavigationNodeIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Action, CandidateId, Cell, LabelId);
    }

    /// <summary>Read-only state supplied to an action generator for one expanded search node.</summary>
    public sealed class NavigationSearchNode
    {
        internal NavigationSearchNode(NavigationNodeIdentity identity, Vector2 position, NavigationSupport support,
            float heuristic)
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
        public float ProgressDistance { get; }

        private NavigationTransition(NavigationNodeIdentity destination, Vector2 destinationPosition, NavigationSupport destinationSupport, NavigationRouteSegment segment, float cost, bool completesGoal, float progressDistance = float.PositiveInfinity)
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
            ProgressDistance = progressDistance;
        }

        public static NavigationTransition GroundSuccessor(int candidateId, Vector2 position, NavigationSupport support, NavigationRouteSegment segment, float cost, bool completesGoal, float progressDistance)
            => new(NavigationNodeIdentity.Ground(candidateId), position, support, segment, cost, completesGoal, progressDistance);

        public static NavigationTransition CompletedJump(Vector2 position, NavigationRouteSegment segment, float cost)
            => new(NavigationNodeIdentity.Jump(-1), position, default, segment, cost, true, 0f);

        public static NavigationTransition JumpLanding(NavigationNodeIdentity destination, Vector2 position, NavigationSupport support, NavigationRouteSegment segment, float cost, bool completesGoal, float progressDistance)
            => new(destination, position, support, segment, cost, completesGoal, progressDistance);

        public static NavigationTransition FlyMove(Vector2Int destinationCell, Vector2 position, NavigationRouteSegment segment, float cost, bool completesGoal, float progressDistance)
            => new(NavigationNodeIdentity.Fly(destinationCell), position, default, segment, cost, completesGoal, progressDistance);
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

    public delegate IEnumerable<NavigationTransitionWork> NavigationTransitionProvider(NavigationSearchNode node);

    /// <summary>Immutable request data consumed by the shared action-graph search.</summary>
    public sealed class NavigationSearchRequest
    {
        public INavigationWorld World { get; }
        public Vector2 Start { get; }
        public NavigationSupport StartSupport { get; }
        public NavigationGoalRegion GoalRegion { get; }
        public Vector2 BodySize { get; }
        public NavigationActions AllowedActions { get; }
        public int MaxExpandedNodes { get; }
        public int MaxTotalWorkUnits { get; }
        public bool AllowExecutablePrefix { get; }
        public NavigationNodeIdentity StartIdentity { get; }
        public Func<Vector2, float> Heuristic { get; }
        public NavigationTransitionProvider Transitions { get; }

        public NavigationSearchRequest(INavigationWorld world, Vector2 start, NavigationSupport startSupport,
            NavigationGoalRegion goalRegion, Vector2 bodySize, NavigationActions allowedActions,
            int maxExpandedNodes, bool allowExecutablePrefix, NavigationNodeIdentity startIdentity,
            NavigationTransitionProvider transitions, Func<Vector2, float> heuristic = null,
            int maxTotalWorkUnits = -1)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            GoalRegion = goalRegion ?? throw new ArgumentNullException(nameof(goalRegion));
            if (!NavigationNumeric.IsFinite(start) || !NavigationNumeric.IsFinite(bodySize) || bodySize.x <= 0f || bodySize.y <= 0f)
                throw new ArgumentException("Navigation search coordinates and body size must be finite and positive.");
            if (maxExpandedNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
            if (allowedActions == 0) throw new ArgumentOutOfRangeException(nameof(allowedActions));
            if (maxTotalWorkUnits == 0 || maxTotalWorkUnits < -1)
                throw new ArgumentOutOfRangeException(nameof(maxTotalWorkUnits));
            Start = start;
            StartSupport = startSupport;
            BodySize = bodySize;
            AllowedActions = allowedActions;
            MaxExpandedNodes = maxExpandedNodes;
            if (maxTotalWorkUnits > 0)
            {
                MaxTotalWorkUnits = maxTotalWorkUnits;
            }
            else
            {
                // Keep synchronous callers bounded even when every candidate needs geometry work.
                int scaledLimit = maxExpandedNodes > 256 ? int.MaxValue : maxExpandedNodes * 16;
                MaxTotalWorkUnits = Math.Min(4096, Math.Max(256, scaledLimit));
            }
            AllowExecutablePrefix = allowExecutablePrefix;
            StartIdentity = startIdentity;
            Transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            Heuristic = heuristic ?? (_ => 0f);
        }

    }

    internal readonly struct NavigationSearchUpdate
    {
        public NavigationSearchStatus Status { get; }
        public NavigationRoute Route { get; }
        public int WorkUnits { get; }
        public int ExpandedNodes { get; }
        public bool IsTerminal { get; }

        private NavigationSearchUpdate(NavigationSearchStatus status, NavigationRoute route,
            int workUnits, int expandedNodes, bool isTerminal)
        {
            Status = status;
            Route = route;
            WorkUnits = workUnits;
            ExpandedNodes = expandedNodes;
            IsTerminal = isTerminal;
        }

        public static NavigationSearchUpdate Pending(int expandedNodes)
            => new(NavigationSearchStatus.Pending, null, 0, expandedNodes, false);

        public static NavigationSearchUpdate CompletedRoute(NavigationRoute route, int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.CompleteRoute, route, workUnits, expandedNodes, true);

        public static NavigationSearchUpdate ExecutablePrefix(NavigationRoute route, int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.ExecutablePrefix, route, workUnits, expandedNodes, true);

        public static NavigationSearchUpdate Exhausted(NavigationRoute route, int workUnits, int expandedNodes)
            => new(NavigationSearchStatus.Exhausted, route, workUnits, expandedNodes, true);

        public static NavigationSearchUpdate BudgetReached(NavigationRoute route, int workUnits, int expandedNodes, bool totalBudgetReached)
            => new(NavigationSearchStatus.BudgetReached, route, workUnits, expandedNodes, totalBudgetReached);
    }

    /// <summary>Cooperatively advances one action-graph search without owning Unity objects.</summary>
    internal sealed class NavigationSearch : IDisposable
    {
        private const float Tolerance = 0.0001f;
        private readonly NavigationSearchRequest request;
        private readonly Dictionary<NavigationNodeIdentity, NavigationSearchNode> nodes = new();
        private readonly NavigationMinHeap<NavigationNodeIdentity> open = new(Comparer<NavigationNodeIdentity>.Create(CompareIdentity));
        private NavigationSearchNode expandingNode;
        private IEnumerator<NavigationTransitionWork> transitionEnumerator;
        private NavigationSearchNode bestNode;
        private bool terminal;
        private bool paused;
        private int expandedNodes;
        private int totalWorkUnits;
        private int disposed;

        internal NavigationSearch(NavigationSearchRequest request)
        {
            this.request = request;
            float heuristic = SafeHeuristic(request.Start);
            NavigationSearchNode start = new(request.StartIdentity, request.Start, request.StartSupport, heuristic)
            {
                BestPath = new PathRecord(null, null, 0f, 0, request.GoalRegion.CompletionDistance(request.Start + Vector2.up * (request.BodySize.y * 0.5f), request.BodySize)),
            };
            nodes.Add(start.Identity, start);
            open.Enqueue(start.Identity, heuristic, heuristic);
            bestNode = start;
        }

        /// <summary>
        /// Advances the search by a bounded slice. A terminal or paused search returns
        /// Pending on subsequent calls and must be discarded or resumed by its owner.
        /// </summary>
        public NavigationSearchUpdate Advance(NavigationWorkBudget budget, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (budget.MaxWorkUnits <= 0)
                throw new ArgumentOutOfRangeException(nameof(budget), "Navigation work budget must allow at least one work unit.");
            if (!NavigationNumeric.IsFinite(budget.Milliseconds)
                || budget.Milliseconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(budget), "Navigation work budget must have a finite positive time slice.");
            if (terminal || paused)
                return NavigationSearchUpdate.Pending(expandedNodes);

            Stopwatch timer = Stopwatch.StartNew();
            int workUnits = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (WorkLimitReached(timer, workUnits, budget))
                    return CreateBudgetUpdate(workUnits, false);

                if (transitionEnumerator == null)
                {
                    if (expandedNodes >= request.MaxExpandedNodes)
                    {
                        if (!HasOpenCandidate())
                        {
                            terminal = true;
                            return NavigationSearchUpdate.Exhausted(BuildBestEffortRoute(true), workUnits, expandedNodes);
                        }

                        return CreateBudgetUpdate(workUnits, true);
                    }

                    if (!TryTakeNext(out expandingNode))
                    {
                        terminal = true;
                        return NavigationSearchUpdate.Exhausted(BuildBestEffortRoute(true), workUnits, expandedNodes);
                    }

                    expandingNode.IsClosed = true;
                    expandedNodes++;
                    transitionEnumerator = request.Transitions(expandingNode)?.GetEnumerator();
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
                    if (WorkLimitReached(timer, workUnits, budget))
                        return CreateBudgetUpdate(workUnits, false);
                    continue;
                }

                NavigationTransition transition = transitionWork.Transition;
                ValidateTransitionResult(transition);
                if (transition.CompletesGoal)
                {
                    terminal = true;
                    return NavigationSearchUpdate.CompletedRoute(BuildRoute(expandingNode, transition, true, true), workUnits, expandedNodes);
                }

                Relax(expandingNode, transition);
                if (totalWorkUnits >= request.MaxTotalWorkUnits)
                    return CreateBudgetUpdate(workUnits, true);
                if (WorkLimitReached(timer, workUnits, budget))
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
            if (request.AllowExecutablePrefix && bestNode?.BestPath?.Step != null)
            {
                paused = true;
                return NavigationSearchUpdate.ExecutablePrefix(BuildPrefixRoute(bestNode), workUnits, expandedNodes);
            }

            return NavigationSearchUpdate.BudgetReached(totalBudgetReached ? BuildBestEffortRoute(false) : null, workUnits, expandedNodes, totalBudgetReached);
        }

        private void Relax(NavigationSearchNode parent, NavigationTransition transition)
        {
            float heuristic = SafeHeuristic(transition.DestinationPosition);
            if (!nodes.TryGetValue(transition.Destination, out NavigationSearchNode destination))
            {
                destination = new NavigationSearchNode(transition.Destination, transition.DestinationPosition, transition.DestinationSupport, heuristic);
                nodes.Add(destination.Identity, destination);
            }

            float progressDistance = float.IsPositiveInfinity(transition.ProgressDistance)
                ? request.GoalRegion.GuidanceDistance(transition.DestinationPosition, request.BodySize)
                : transition.ProgressDistance;
            PathRecord candidatePath = new(parent.BestPath, transition.Segment, parent.BestPath.RouteCost + transition.Cost, parent.BestPath.StepCount + 1, progressDistance);
            bool better = destination.BestPath == null || IsBetterPath(candidatePath, destination.BestPath);
            if (!better) return;

            destination.Heuristic = heuristic;
            destination.BestPath = candidatePath;
            destination.IsClosed = false;
            open.Enqueue(destination.Identity, candidatePath.RouteCost + heuristic, heuristic);

            if (destination.BestPath.Step != null && IsBetterProgress(destination, bestNode))
                bestNode = destination;
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

        private NavigationRoute BuildPrefixRoute(NavigationSearchNode node)
        {
            PathRecord firstPath = node.BestPath;
            while (firstPath.Parent != null && firstPath.Parent.Step != null)
                firstPath = firstPath.Parent;
            NavigationRouteSegment first = firstPath.Step;

            return NavigationRoute.Partial(request.Start, request.GoalRegion, first.End, new[] { first });
        }

        private NavigationRoute BuildBestEffortRoute(bool searchComplete)
            => bestNode?.BestPath?.Step == null ? null : BuildRoute(bestNode, default, false, searchComplete);

        private NavigationRoute BuildRoute(NavigationSearchNode node, NavigationTransition terminalTransition, bool includeTerminal, bool searchComplete)
        {
            List<NavigationRouteSegment> reversed = new();
            if (includeTerminal) reversed.Add(terminalTransition.Segment);

            for (PathRecord path = node.BestPath; path != null && path.Step != null; path = path.Parent)
            {
                reversed.Add(path.Step);
            }

            reversed.Reverse();
            Vector2 resolvedGoal = includeTerminal ? terminalTransition.DestinationPosition : node.Position;
            return searchComplete
                ? NavigationRoute.Complete(request.Start, request.GoalRegion, resolvedGoal, reversed)
                : NavigationRoute.Partial(request.Start, request.GoalRegion, resolvedGoal, reversed);
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
            float heuristic = request.Heuristic(position);
            return !NavigationNumeric.IsFinite(heuristic) || heuristic < 0f ? 0f : heuristic;
        }

        private static bool IsBetterProgress(NavigationSearchNode candidate, NavigationSearchNode best)
        {
            if (candidate.BestPath.ProgressDistance < best.BestPath.ProgressDistance - Tolerance) return true;
            if (Mathf.Abs(candidate.BestPath.ProgressDistance - best.BestPath.ProgressDistance) > Tolerance) return false;
            if (candidate.BestPath.RouteCost < best.BestPath.RouteCost - Tolerance) return true;
            if (Mathf.Abs(candidate.BestPath.RouteCost - best.BestPath.RouteCost) > Tolerance) return false;
            if (candidate.BestPath.StepCount != best.BestPath.StepCount)
                return candidate.BestPath.StepCount < best.BestPath.StepCount;
            return CompareIdentity(candidate.Identity, best.Identity) < 0;
        }

        private static bool IsBetterPath(PathRecord candidate, PathRecord best)
            => candidate.RouteCost < best.RouteCost - Tolerance || (Mathf.Abs(candidate.RouteCost - best.RouteCost) <= Tolerance && candidate.StepCount < best.StepCount);

        private static bool WorkLimitReached(Stopwatch timer, int workUnits, NavigationWorkBudget budget)
            => workUnits >= budget.MaxWorkUnits || timer.Elapsed.TotalMilliseconds >= budget.Milliseconds;

        private static int CompareIdentity(NavigationNodeIdentity left, NavigationNodeIdentity right)
        {
            int action = ((int)left.Action).CompareTo((int)right.Action);
            if (action != 0) return action;
            int candidate = left.CandidateId.CompareTo(right.CandidateId);
            if (candidate != 0) return candidate;
            int y = left.Cell.y.CompareTo(right.Cell.y);
            if (y != 0) return y;
            int x = left.Cell.x.CompareTo(right.Cell.x);
            return x != 0 ? x : left.LabelId.CompareTo(right.LabelId);
        }


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
            public readonly float ProgressDistance;

            public PathRecord(PathRecord parent, NavigationRouteSegment step, float routeCost, int stepCount,
                float progressDistance)
            {
                Parent = parent;
                Step = step;
                RouteCost = routeCost;
                StepCount = stepCount;
                ProgressDistance = progressDistance;
            }
        }
    }
}
