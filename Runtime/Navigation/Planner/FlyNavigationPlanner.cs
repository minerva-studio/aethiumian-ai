using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Performs deterministic bounded aerial planning around solid world geometry.</summary>
    public sealed class FlyNavigationPlanner : NavigationPlanner<FlyNavigationParameters>
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down, };
        private static readonly ProfilerMarker SearchMarker = new("AethiumianAI.Navigation.FlySearch");

        private const float FlightStep = NavigationConstant.FlightStep;

        /// <summary>Creates a planner with an explicit expansion limit.</summary>
        public FlyNavigationPlanner(INavigationWorld world, int maxExpandedNodes) : base(world, maxExpandedNodes)
        {
        }

        /// <summary>Converts a world position into this planner's own search-lattice coordinate.</summary>
        private static Vector2Int ToLattice(INavigationWorld world, Vector2 position)
        {
            Vector2 local = (position - world.WorldBounds.min) / FlightStep;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        /// <summary>Gets the world position of one search-lattice cell center.</summary>
        private static Vector2 LatticeCenter(INavigationWorld world, Vector2Int cell)
            => world.WorldBounds.min + ((Vector2)cell + Vector2.one * 0.5f) * FlightStep;

        /// <summary>Returns whether a lattice cell center still belongs to the captured world.</summary>
        private static bool IsInsideWorld(INavigationWorld world, Vector2 position) => world.WorldBounds.Contains(position);

        /// <summary>Runs aerial planning through the shared action-graph search.</summary>
        public override NavigationPlanResult Plan(Vector2 start, NavigationGoalRequest goal, FlyNavigationParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null)
        {
            ValidatePlanInputs(start, cancellationToken);
            ValidateParameters(parameters);
            if (!World.IsCenteredBodyClearAt(start, parameters.BodySize))
                return NavigationPlanResult.NoResult;

            if (goal.IsRetreat)
                return PlanRetreat(start, goal, parameters, cancellationToken, diagnostics);

            Vector2 resolvedGoal = default;
            bool hasGoal = false;
            foreach (Vector2? candidate in EnumerateGoalResolution(goal, parameters))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.HasValue)
                {
                    resolvedGoal = candidate.Value;
                    hasGoal = true;
                }
            }
            if (!hasGoal)
                return NavigationPlanResult.NoResult;
            if (start.Equals(resolvedGoal))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goal, World, resolvedGoal, Array.Empty<NavigationRouteSegment>()));

            bool directClear = false;
            foreach (bool? segmentClear in EnumerateCenteredSegmentClear(World, start, resolvedGoal,
                parameters.BodySize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (segmentClear.HasValue)
                {
                    directClear = segmentClear.Value;
                    break;
                }
            }
            if (directClear)
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goal, World,
                    resolvedGoal, new[] { new FlyRouteSegment(start, resolvedGoal) }));

            if (!TryFindConnectorCell(World, start, parameters.BodySize, out Vector2Int startCell)
                || !TryFindConnectorCell(World, resolvedGoal, parameters.BodySize, out Vector2Int goalCell))
                return NavigationPlanResult.NoResult;

            NavigationSearchRequest request = new(World, start, default, goal, parameters.BodySize,
                NavigationActions.Fly, MaxExpandedNodes, NavigationNodeIdentity.Fly(startCell),
                node => EnumerateFlyTransitions(node, goal, resolvedGoal, goalCell, parameters),
                _ => 0f);
            return RunSearch(request, diagnostics, cancellationToken);
        }

        /// <summary>Tests direct flight, then one set of local neighbours; never runs full search.</summary>
        public override NavigationPlanResult PlanSingleStep(Vector2 start, NavigationGoalRequest goal, FlyNavigationParameters parameters, CancellationToken cancellationToken = default)
        {
            ValidatePlanInputs(start, cancellationToken);
            ValidateParameters(parameters);
            if (!World.IsCenteredBodyClearAt(start, parameters.BodySize)) return NavigationPlanResult.NoResult;
            if (World.IsGoalComplete(goal, start, parameters.BodySize))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goal, World, start, Array.Empty<NavigationRouteSegment>()));
            Vector2 direct = GetGoalCenter(goal, parameters.BodySize);
            if (goal.IsRetreat)
            {
                Vector2 away = start - goal.Center;
                direct = start + away.normalized * Mathf.Max(NavigationConstant.MinimumRetreatStep, goal.RetreatDistance + parameters.BodySize.magnitude);
            }
            if (World.IsGoalComplete(goal, direct, parameters.BodySize) && LocalStepAllowed(start, direct, goal, parameters))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goal, World, direct, new[] { new FlyRouteSegment(start, direct) }));
            Vector2? best = null;
            float bestDistance = goal.GuidanceDistance(start, parameters.BodySize);
            Vector2Int cell = ToLattice(World, start);
            foreach (Vector2Int direction in Directions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Vector2Int nextCell = cell + direction;
                Vector2 next = LatticeCenter(World, nextCell);
                if (!IsInsideWorld(World, next)) continue;
                if (!LocalStepAllowed(start, next, goal, parameters)) continue;
                float distance = goal.GuidanceDistance(next, parameters.BodySize);
                if (!IsStrictlyLess(distance, bestDistance)) continue;
                bestDistance = distance;
                best = next;
            }
            if (!best.HasValue) return NavigationPlanResult.NoResult;
            Vector2 endpoint = best.Value;
            return NavigationPlanResult.ResultProduced(NavigationRoute.Create(start, goal, World, endpoint,
                new[] { new FlyRouteSegment(start, endpoint) }, World.IsGoalComplete(goal, endpoint, parameters.BodySize)));
        }

        private bool LocalStepAllowed(Vector2 start, Vector2 end, NavigationGoalRequest goal, FlyNavigationParameters parameters)
            => World.IsCenteredBodyClearAt(end, parameters.BodySize)
                && World.IsCenteredBodySegmentClear(start, end, parameters.BodySize)
                && (!goal.IsRetreat || !parameters.HasApproachLimit || RetreatNavigationGeometry.SegmentApproachDistance(start, end, goal.Center) <= parameters.RemainingApproachDistance);

        private IEnumerable<NavigationTransitionWork> EnumerateFlyTransitions(NavigationSearchNode node, NavigationGoalRequest goal, Vector2 resolvedGoal, Vector2Int goalCell, FlyNavigationParameters parameters)
        {
            Vector2 source = node.Position;
            Vector2Int currentCell = node.Identity.Cell;
            if (!source.Equals(LatticeCenter(World, currentCell)))
            {
                // The first edge starts at the real body position; all later edges start at lattice centers.
            }

            for (int index = 0; index < Directions.Length; index++)
            {
                yield return NavigationTransitionWork.WorkUnit;
                Vector2Int next = currentCell + Directions[index];
                Vector2 center = LatticeCenter(World, next);
                if (!IsInsideWorld(World, center)) continue;
                Vector2 destination = next == goalCell ? resolvedGoal : center;
                if (!World.IsCenteredBodyClearAt(destination, parameters.BodySize) || !World.IsCenteredBodySegmentClear(source, destination, parameters.BodySize))
                    continue;
                bool completesGoal = next == goalCell && World.IsGoalComplete(goal, destination, parameters.BodySize);
                yield return NavigationTransitionWork.Edge(NavigationTransition.FlyMove(next, destination, new FlyRouteSegment(source, destination), Vector2.Distance(source, destination), completesGoal));
            }
        }

        /// <summary>Scans goal cells with explicit cancellation boundaries and returns the nearest clear center.</summary>
        private IEnumerable<Vector2?> EnumerateGoalResolution(NavigationGoalRequest goal, FlyNavigationParameters parameters)
        {
            Vector2 requestedCenter = GetGoalCenter(goal, parameters.BodySize);
            if (World.IsGoalComplete(goal, requestedCenter, parameters.BodySize) && World.IsCenteredBodyClearAt(requestedCenter, parameters.BodySize))
            {
                yield return requestedCenter;
                yield break;
            }

            Vector2 resolvedGoal = default;
            float bestDistanceSquared = float.PositiveInfinity;
            AABB target = goal.TargetBounds;
            int xMin = Mathf.FloorToInt((target.Min.x - parameters.BodySize.x * 0.5f - goal.ArrivalTolerance - World.WorldBounds.xMin) / FlightStep) - 1;
            int xMax = Mathf.CeilToInt((target.Max.x + parameters.BodySize.x * 0.5f + goal.ArrivalTolerance - World.WorldBounds.xMin) / FlightStep) + 1;
            int yMin = Mathf.FloorToInt((target.Min.y - parameters.BodySize.y * 0.5f - goal.ArrivalTolerance - World.WorldBounds.yMin) / FlightStep) - 1;
            int yMax = Mathf.CeilToInt((target.Max.y + parameters.BodySize.y * 0.5f + goal.ArrivalTolerance - World.WorldBounds.yMin) / FlightStep) + 1;
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    Vector2 candidate = LatticeCenter(World, cell);
                    if (IsInsideWorld(World, candidate))
                    {
                        Vector2 candidateCenter = GetGoalCenter(goal, parameters.BodySize, candidate);
                        float distance = World.GetGoalCompletionDistance(goal, candidateCenter, parameters.BodySize);
                        float distanceSquared = distance * distance;
                        if (World.IsGoalComplete(goal, candidateCenter, parameters.BodySize)
                            && distanceSquared + Tolerance < bestDistanceSquared
                            && World.IsCenteredBodyClearAt(candidateCenter, parameters.BodySize))
                        {
                            bestDistanceSquared = distanceSquared;
                            resolvedGoal = candidateCenter;
                        }
                    }

                    yield return null;
                }
            }

            yield return float.IsPositiveInfinity(bestDistanceSquared) ? (Vector2?)null : resolvedGoal;
        }

        /// <summary>Searches the same aerial grid until any clear cell satisfies the Retreat predicate.</summary>
        private NavigationPlanResult PlanRetreat(Vector2 start, NavigationGoalRequest goal, FlyNavigationParameters parameters, CancellationToken cancellationToken, NavigationPlanningDiagnostics diagnostics)
        {
            if (World.IsGoalComplete(goal, start, parameters.BodySize))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goal, World, start,
                    Array.Empty<NavigationRouteSegment>()));

            if (!TryFindRetreatConnectorCell(World, start, goal.Center, parameters, out Vector2Int startCell))
                return NavigationPlanResult.NoResult;

            Vector2 startCellPosition = LatticeCenter(World, startCell);
            float startApproach = RetreatNavigationGeometry.SegmentApproachDistance(start, startCellPosition, goal.Center);
            if (parameters.HasApproachLimit && startApproach > parameters.RemainingApproachDistance + Tolerance)
                return NavigationPlanResult.NoResult;

            List<RetreatSearchLabel> labels = new();
            Dictionary<Vector2Int, List<int>> labelIdsByCell = new();
            NavigationMinHeap<HeapKey> open = new();
            float startHeuristic = World.GetGoalCompletionDistance(goal, startCellPosition, parameters.BodySize);
            RetreatSearchLabel startLabel = new(startCell, -1, Vector2.Distance(start, startCellPosition), startApproach, startHeuristic);
            labels.Add(startLabel);
            labelIdsByCell.Add(startCell, new List<int> { 0 });
            open.Enqueue(new(startCell, 0), startLabel.RetreatScore, startHeuristic);
            int expanded = 0;

            while (expanded < MaxExpandedNodes && TrySelectRetreatNext(open, labels, out int currentLabelId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                RetreatSearchLabel currentLabel = labels[currentLabelId];
                currentLabel.State = RetreatSearchLabelState.Closed;
                Vector2 currentPosition = LatticeCenter(World, currentLabel.Cell);
                if (World.IsGoalComplete(goal, currentPosition, parameters.BodySize))
                {
                    return NavigationPlanResult.ResultProduced(BuildRetreatRoute(start, goal, World, labels, currentLabelId));
                }

                expanded++;
                diagnostics?.RecordPathExpansion();
                using (SearchMarker.Auto()) { }

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = currentLabel.Cell + Directions[i];
                    Vector2 nextPosition = LatticeCenter(World, next);
                    if (!IsInsideWorld(World, nextPosition)) continue;
                    if (!World.IsCenteredBodyClearAt(nextPosition, parameters.BodySize) || !World.IsCenteredBodySegmentClear(currentPosition, nextPosition, parameters.BodySize)) continue;

                    float candidateCost = currentLabel.PathCost + FlightStep;
                    float candidateApproach = currentLabel.ApproachCost + RetreatNavigationGeometry.SegmentApproachDistance(currentPosition, nextPosition, goal.Center);
                    if (parameters.HasApproachLimit && candidateApproach > parameters.RemainingApproachDistance + Tolerance) continue;
                    RetreatSearchLabel candidate = new(next, currentLabelId, candidateCost, candidateApproach, World.GetGoalCompletionDistance(goal, nextPosition, parameters.BodySize));
                    if (!TryAddRetreatLabel(labels, labelIdsByCell, candidate, out int candidateId)) continue;
                    open.Enqueue(new(next, candidateId), candidate.RetreatScore, candidate.Heuristic);
                }
            }

            bool hasOpenFrontier = HasOpenRetreatLabel(labels);
            if (!hasOpenFrontier)
                return NavigationPlanResult.SearchExhausted();

            return NavigationPlanResult.BudgetReached();
        }

        private static NavigationRoute BuildRetreatRoute(Vector2 start, NavigationGoalRequest goal, INavigationWorld snapshot, List<RetreatSearchLabel> labels, int labelId)
        {
            List<int> chain = new();
            for (int current = labelId; current >= 0; current = labels[current].ParentId)
            {
                chain.Add(current);
            }
            chain.Reverse();

            List<Vector2Int> cells = new(chain.Count);
            for (int index = 0; index < chain.Count; index++)
                cells.Add(labels[chain[index]].Cell);
            return BuildFlyRoute(start, goal, snapshot, cells, null);
        }

        private static NavigationRoute BuildFlyRoute(Vector2 start, NavigationGoalRequest goal, INavigationWorld snapshot, IReadOnlyList<Vector2Int> cells, Vector2? finalEndpoint)
        {
            List<NavigationRouteSegment> segments = new();
            Vector2 previous = start;
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2 next = LatticeCenter(snapshot, cells[index]);
                if (next == previous) continue;
                segments.Add(new FlyRouteSegment(previous, next));
                previous = next;
            }

            if (finalEndpoint.HasValue && !previous.Equals(finalEndpoint.Value))
            {
                segments.Add(new FlyRouteSegment(previous, finalEndpoint.Value));
                previous = finalEndpoint.Value;
            }

            return NavigationRoute.Complete(start, goal, snapshot, previous, segments);
        }

        private static bool TryAddRetreatLabel(List<RetreatSearchLabel> labels, Dictionary<Vector2Int, List<int>> labelIdsByCell, RetreatSearchLabel candidate, out int candidateId)
        {
            candidateId = -1;
            if (!labelIdsByCell.TryGetValue(candidate.Cell, out List<int> cellLabelIds))
            {
                cellLabelIds = new List<int>();
                labelIdsByCell.Add(candidate.Cell, cellLabelIds);
            }

            for (int index = 0; index < cellLabelIds.Count; index++)
            {
                RetreatSearchLabel existing = labels[cellLabelIds[index]];
                if (existing.State == RetreatSearchLabelState.Discarded) continue;
                bool existingNoWorse = existing.PathCost <= candidate.PathCost + Tolerance && existing.ApproachCost <= candidate.ApproachCost + Tolerance;
                if (existingNoWorse) return false;
            }

            for (int index = 0; index < cellLabelIds.Count; index++)
            {
                RetreatSearchLabel existing = labels[cellLabelIds[index]];
                if (existing.State == RetreatSearchLabelState.Discarded) continue;
                if (candidate.PathCost <= existing.PathCost + Tolerance
                    && candidate.ApproachCost <= existing.ApproachCost + Tolerance)
                {
                    existing.State = RetreatSearchLabelState.Discarded;
                }
            }

            candidateId = labels.Count;
            labels.Add(candidate);
            cellLabelIds.Add(candidateId);
            return true;
        }

        private static bool TrySelectRetreatNext(NavigationMinHeap<HeapKey> open, List<RetreatSearchLabel> labels, out int labelId)
        {
            labelId = -1;
            while (!open.IsEmpty)
            {
                // open.Dequeue(out float queuedScore, out int queuedLabelId);
                var key = open.Dequeue(out float queuedScore);
                var queuedLabelId = key.LabelId;
                if (queuedLabelId < 0 || queuedLabelId >= labels.Count) continue;
                RetreatSearchLabel label = labels[queuedLabelId];
                if (label.State != RetreatSearchLabelState.Open) continue;
                if (Mathf.Abs(queuedScore - label.RetreatScore) > Tolerance) continue;
                labelId = queuedLabelId;
                return true;
            }

            return false;
        }

        private static bool HasOpenRetreatLabel(List<RetreatSearchLabel> labels)
        {
            for (int index = 0; index < labels.Count; index++)
            {
                RetreatSearchLabel label = labels[index];
                if (label.State == RetreatSearchLabelState.Open) return true;
            }

            return false;
        }

        private static bool TryFindRetreatConnectorCell(INavigationWorld snapshot, Vector2 position, Vector2 targetCenter, FlyNavigationParameters parameters, out Vector2Int connector)
        {
            connector = default;
            Vector2Int origin = ToLattice(snapshot, position);
            float bestApproach = float.PositiveInfinity;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int y = origin.y - 1; y <= origin.y + 1; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + 1; x++)
                {
                    Vector2Int cell = new(x, y);
                    Vector2 center = LatticeCenter(snapshot, cell);
                    if (!IsInsideWorld(snapshot, center)) continue;
                    if (!snapshot.IsCenteredBodyClearAt(center, parameters.BodySize) || !snapshot.IsCenteredBodySegmentClear(position, center, parameters.BodySize)) continue;
                    float approach = RetreatNavigationGeometry.SegmentApproachDistance(position, center, targetCenter);
                    if (parameters.HasApproachLimit && approach > parameters.RemainingApproachDistance + Tolerance) continue;
                    float distanceSquared = (center - position).sqrMagnitude;
                    if (approach > bestApproach + Tolerance || (Mathf.Abs(approach - bestApproach) <= Tolerance && distanceSquared >= bestDistanceSquared - Tolerance)) continue;
                    bestApproach = approach;
                    bestDistanceSquared = distanceSquared;
                    connector = cell;
                }
            }

            return !float.IsPositiveInfinity(bestApproach);
        }

        /// <summary>Converts a Ground Range lower-center goal into the center anchor used by Fly.</summary>
        private static Vector2 GetGoalCenter(NavigationGoalRequest goal, Vector2 bodySize) => GetGoalCenter(goal, bodySize, goal.Center);

        /// <summary>Converts one resolved lower-center candidate into Fly's center-anchor space.</summary>
        private static Vector2 GetGoalCenter(NavigationGoalRequest goal, Vector2 bodySize, Vector2 candidate) => goal.IsGroundWalk ? candidate + Vector2.up * (bodySize.y * 0.5f) : candidate;

        /// <summary>Checks center-anchored segment samples with explicit cancellation boundaries.</summary>
        private static IEnumerable<bool?> EnumerateCenteredSegmentClear(INavigationWorld snapshot, Vector2 start, Vector2 end, Vector2 bodySize)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / NavigationConstant.MaximumTraversalSampleSpacing));
            bool clear = true;
            for (int i = 0; i <= samples; i++)
            {
                if (clear)
                    clear = snapshot.IsCenteredBodyClearAt(Vector2.Lerp(start, end, i / (float)samples), bodySize);
                yield return null;
            }

            yield return clear;
        }

        /// <summary>Finds the nearest clear grid connector for a physical center position.</summary>
        private static bool TryFindConnectorCell(INavigationWorld snapshot, Vector2 position, Vector2 bodySize, out Vector2Int connector)
        {
            connector = default;
            Vector2Int origin = ToLattice(snapshot, position);
            float bestDistanceSquared = float.PositiveInfinity;
            for (int y = origin.y - 1; y <= origin.y + 1; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + 1; x++)
                {
                    Vector2Int cell = new(x, y);
                    Vector2 center = LatticeCenter(snapshot, cell);
                    if (!IsInsideWorld(snapshot, center)) continue;
                    float distanceSquared = (center - position).sqrMagnitude;
                    if (distanceSquared + Tolerance >= bestDistanceSquared
                        || !snapshot.IsCenteredBodyClearAt(center, bodySize)
                        || !snapshot.IsCenteredBodySegmentClear(position, center, bodySize)) continue;
                    bestDistanceSquared = distanceSquared;
                    connector = cell;
                }
            }

            return !float.IsPositiveInfinity(bestDistanceSquared);
        }

        /// <summary>Validates the physical aerial profile at the planner boundary.</summary>
        private static void ValidateParameters(FlyNavigationParameters parameters)
        {
            Validate.PositiveVector(parameters.BodySize, nameof(parameters));
            if (!NavigationNumeric.IsFinite(parameters.RemainingApproachDistance) || parameters.RemainingApproachDistance < 0f
                || !parameters.HasApproachLimit && parameters.RemainingApproachDistance != 0f)
                throw new ArgumentException("Fly retreat approach budget must be finite and non-negative.", nameof(parameters));
        }


        private readonly struct HeapKey : IComparable<HeapKey>
        {
            public Vector2Int Cell { get; }
            public int LabelId { get; }

            public HeapKey(Vector2Int cell, int labelId)
            {
                Cell = cell;
                LabelId = labelId;
            }

            public readonly int CompareTo(HeapKey other)
            {
                int y = Cell.y.CompareTo(other.Cell.y);
                if (y != 0) return y;
                int x = Cell.x.CompareTo(other.Cell.x);
                return x != 0 ? x : LabelId.CompareTo(other.LabelId);
            }
        }

        private sealed class RetreatSearchLabel
        {
            public readonly Vector2Int Cell;
            public readonly int ParentId;
            public readonly float PathCost;
            public readonly float ApproachCost;
            public readonly float Heuristic;
            public RetreatSearchLabelState State = RetreatSearchLabelState.Open;

            /// <summary>Calculates the retreat score for a given label.</summary>
            public float RetreatScore => PathCost + ApproachCost + Heuristic;

            public RetreatSearchLabel(Vector2Int cell, int parentId, float pathCost,
                float approachCost, float heuristic)
            {
                Cell = cell;
                ParentId = parentId;
                PathCost = pathCost;
                ApproachCost = approachCost;
                Heuristic = heuristic;
            }
        }

        private enum RetreatSearchLabelState
        {
            Open,
            Closed,
            Discarded,
        }
    }
}
