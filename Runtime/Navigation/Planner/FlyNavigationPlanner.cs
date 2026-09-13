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

        /// <summary>Creates a planner with an explicit expansion limit.</summary>
        public FlyNavigationPlanner(INavigationWorld world, int maxExpandedNodes) : base(world, maxExpandedNodes)
        {
        }

        /// <summary>Runs aerial planning through the shared action-graph search.</summary>
        public override NavigationPlanResult Plan(Vector2 start, NavigationGoalRegion goalRegion,
            FlyNavigationParameters parameters, CancellationToken cancellationToken = default,
            NavigationPlanningDiagnostics diagnostics = null, bool allowExecutablePrefix = false)
        {
            ValidatePlanInputs(start, goalRegion, cancellationToken);
            ValidateParameters(parameters);
            if (!World.IsCenteredBodyClearAt(start, parameters.BodySize))
                return NavigationPlanResult.NoResult;

            if (goalRegion.IsRetreat)
                return PlanRetreat(start, goalRegion, parameters, cancellationToken, diagnostics);

            Vector2 resolvedGoal = default;
            bool hasGoal = false;
            foreach (Vector2? candidate in EnumerateGoalResolution(goalRegion, parameters))
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
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goalRegion,
                    resolvedGoal, Array.Empty<NavigationRouteSegment>()));

            bool directClear = false;
            foreach (bool? segmentClear in EnumerateCenteredSegmentClear(World, start, resolvedGoal,
                parameters.BodySize, 0.2f))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (segmentClear.HasValue)
                {
                    directClear = segmentClear.Value;
                    break;
                }
            }
            if (directClear)
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goalRegion,
                    resolvedGoal, new[] { new FlyRouteSegment(start, resolvedGoal) }));

            if (!TryFindConnectorCell(World, start, parameters.BodySize, out Vector2Int startCell)
                || !TryFindConnectorCell(World, resolvedGoal, parameters.BodySize, out Vector2Int goalCell))
                return NavigationPlanResult.NoResult;

            NavigationSearchRequest request = new(World, start, default, goalRegion, parameters.BodySize,
                NavigationActions.Fly, MaxExpandedNodes, allowExecutablePrefix, NavigationNodeIdentity.Fly(startCell),
                node => EnumerateFlyTransitions(node, goalRegion, resolvedGoal, goalCell, parameters),
                _ => 0f);
            return RunSearch(request, diagnostics, cancellationToken);
        }

        private IEnumerable<NavigationTransitionWork> EnumerateFlyTransitions(NavigationSearchNode node,
            NavigationGoalRegion goalRegion, Vector2 resolvedGoal, Vector2Int goalCell,
            FlyNavigationParameters parameters)
        {
            Vector2 source = node.Position;
            Vector2Int currentCell = node.Identity.Cell;
            if (!source.Equals(NavigationWorldQueries.CellCenter(World, currentCell)))
            {
                // The first edge starts at the real body position; all later edges start at cell centers.
            }

            for (int index = 0; index < Directions.Length; index++)
            {
                yield return NavigationTransitionWork.WorkUnit;
                Vector2Int next = currentCell + Directions[index];
                if (!World.CellBounds.Contains(next)) continue;
                Vector2 destination = next == goalCell
                    ? resolvedGoal : NavigationWorldQueries.CellCenter(World, next);
                if (!World.IsCenteredBodyClearAt(destination, parameters.BodySize)
                    || !World.IsCenteredBodySegmentClear(source, destination, parameters.BodySize, 0.2f))
                    continue;
                bool completesGoal = next == goalCell && goalRegion.IsComplete(destination, parameters.BodySize);
                yield return NavigationTransitionWork.Edge(NavigationTransition.FlyMove(next, destination,
                    new FlyRouteSegment(source, destination), Vector2.Distance(source, destination),
                    completesGoal, goalRegion.GuidanceDistance(destination, parameters.BodySize)));
            }
        }

        /// <summary>Scans goal cells with explicit cancellation boundaries and returns the nearest clear center.</summary>
        private IEnumerable<Vector2?> EnumerateGoalResolution(NavigationGoalRegion goalRegion,
            FlyNavigationParameters parameters)
        {
            Vector2 requestedCenter = GetGoalCenter(goalRegion, parameters.BodySize);
            if (goalRegion.IsComplete(requestedCenter, parameters.BodySize)
                && World.IsCenteredBodyClearAt(requestedCenter, parameters.BodySize))
            {
                yield return requestedCenter;
                yield break;
            }

            Vector2 resolvedGoal = default;
            float bestDistanceSquared = float.PositiveInfinity;
            Bounds target = goalRegion.TargetBounds;
            int xMin = Mathf.FloorToInt((target.min.x - parameters.BodySize.x * 0.5f - goalRegion.ArrivalErrorBound - World.Origin.x) / World.CellSize) - 1;
            int xMax = Mathf.CeilToInt((target.max.x + parameters.BodySize.x * 0.5f + goalRegion.ArrivalErrorBound - World.Origin.x) / World.CellSize) + 1;
            int yMin = Mathf.FloorToInt((target.min.y - parameters.BodySize.y * 0.5f - goalRegion.ArrivalErrorBound - World.Origin.y) / World.CellSize) - 1;
            int yMax = Mathf.CeilToInt((target.max.y + parameters.BodySize.y * 0.5f + goalRegion.ArrivalErrorBound - World.Origin.y) / World.CellSize) + 1;
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (World.CellBounds.Contains(cell))
                    {
                        Vector2 candidate = NavigationWorldQueries.CellCenter(World, cell);
                        Vector2 candidateCenter = GetGoalCenter(goalRegion, parameters.BodySize, candidate);
                        float distance = goalRegion.CompletionDistance(candidateCenter, parameters.BodySize);
                        float distanceSquared = distance * distance;
                        if (goalRegion.IsComplete(candidateCenter, parameters.BodySize)
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
        private NavigationPlanResult PlanRetreat(Vector2 start, NavigationGoalRegion goalRegion,
            FlyNavigationParameters parameters, CancellationToken cancellationToken,
            NavigationPlanningDiagnostics diagnostics)
        {
            if (goalRegion.IsComplete(start, parameters.BodySize))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goalRegion, start,
                    Array.Empty<NavigationRouteSegment>()));

            if (!TryFindRetreatConnectorCell(World, start, goalRegion.Center, parameters, out Vector2Int startCell))
                return NavigationPlanResult.NoResult;

            Vector2 startCellPosition = NavigationWorldQueries.CellCenter(World, startCell);
            float startApproach = RetreatNavigationGeometry.SegmentApproachDistance(start, startCellPosition, goalRegion.Center);
            if (parameters.HasApproachLimit && startApproach > parameters.RemainingApproachDistance + Tolerance)
                return NavigationPlanResult.NoResult;

            List<RetreatSearchLabel> labels = new();
            Dictionary<Vector2Int, List<int>> labelIdsByCell = new();
            NavigationCellMinHeap open = new();
            float startHeuristic = goalRegion.CompletionDistance(startCellPosition, parameters.BodySize);
            RetreatSearchLabel startLabel = new(startCell, -1, Vector2.Distance(start, startCellPosition), startApproach, startHeuristic);
            labels.Add(startLabel);
            labelIdsByCell.Add(startCell, new List<int> { 0 });
            open.Enqueue(startCell, startLabel.RetreatScore, startHeuristic, 0);
            float startRemaining = goalRegion.CompletionDistance(start, parameters.BodySize);
            int expanded = 0;

            while (expanded < MaxExpandedNodes && TrySelectRetreatNext(open, labels, out int currentLabelId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                RetreatSearchLabel currentLabel = labels[currentLabelId];
                currentLabel.State = RetreatSearchLabelState.Closed;
                Vector2 currentPosition = NavigationWorldQueries.CellCenter(World, currentLabel.Cell);
                if (goalRegion.IsComplete(currentPosition, parameters.BodySize))
                {
                    return NavigationPlanResult.ResultProduced(BuildRetreatRoute(start, goalRegion, World,
                        labels, currentLabelId));
                }

                expanded++;
                diagnostics?.RecordPathExpansion();
                using (SearchMarker.Auto()) { }

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = currentLabel.Cell + Directions[i];
                    if (!World.CellBounds.Contains(next)) continue;
                    Vector2 nextPosition = NavigationWorldQueries.CellCenter(World, next);
                    if (!World.IsCenteredBodyClearAt(nextPosition, parameters.BodySize)
                        || !World.IsCenteredBodySegmentClear(currentPosition, nextPosition, parameters.BodySize, 0.2f)) continue;

                    float candidateCost = currentLabel.PathCost + World.CellSize;
                    float candidateApproach = currentLabel.ApproachCost
                        + RetreatNavigationGeometry.SegmentApproachDistance(
                            currentPosition, nextPosition, goalRegion.Center);
                    if (parameters.HasApproachLimit && candidateApproach > parameters.RemainingApproachDistance + Tolerance) continue;
                    RetreatSearchLabel candidate = new(next, currentLabelId, candidateCost, candidateApproach, goalRegion.CompletionDistance(nextPosition, parameters.BodySize));
                    if (!TryAddRetreatLabel(labels, labelIdsByCell, candidate, out int candidateId)) continue;
                    open.Enqueue(next, candidate.RetreatScore, candidate.Heuristic, candidateId);
                }
            }

            bool hasOpenFrontier = HasOpenRetreatLabel(labels);
            if (!hasOpenFrontier)
                return NavigationPlanResult.SearchExhausted();

            if (TryFindBestRetreatFrontier(labels, World, goalRegion, parameters, startRemaining, out int frontierLabelId))
                return NavigationPlanResult.BudgetReached(BuildRetreatRoute(start, goalRegion, World, labels,
                    frontierLabelId, false));

            return NavigationPlanResult.BudgetReached();
        }

        private static NavigationRoute BuildRetreatRoute(Vector2 start, NavigationGoalRegion goalRegion,
            INavigationWorld snapshot, List<RetreatSearchLabel> labels, int labelId, bool searchComplete = true)
        {
            List<int> chain = new();
            for (int current = labelId; current >= 0; current = labels[current].ParentId)
                chain.Add(current);
            chain.Reverse();

            List<Vector2Int> cells = new(chain.Count);
            for (int index = 0; index < chain.Count; index++)
                cells.Add(labels[chain[index]].Cell);
            return BuildFlyRoute(start, goalRegion, snapshot, cells, null, searchComplete);
        }

        private static NavigationRoute BuildFlyRoute(Vector2 start, NavigationGoalRegion goalRegion, INavigationWorld snapshot, IReadOnlyList<Vector2Int> cells, Vector2? finalEndpoint, bool searchComplete = true)
        {
            List<NavigationRouteSegment> segments = new();
            Vector2 previous = start;
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2 next = NavigationWorldQueries.CellCenter(snapshot, cells[index]);
                if (next == previous) continue;
                segments.Add(new FlyRouteSegment(previous, next));
                previous = next;
            }

            if (finalEndpoint.HasValue && !previous.Equals(finalEndpoint.Value))
            {
                segments.Add(new FlyRouteSegment(previous, finalEndpoint.Value));
                previous = finalEndpoint.Value;
            }

            return searchComplete
                ? NavigationRoute.Complete(start, goalRegion, previous, segments)
                : NavigationRoute.Partial(start, goalRegion, previous, segments);
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

        private static bool TrySelectRetreatNext(NavigationCellMinHeap open, List<RetreatSearchLabel> labels, out int labelId)
        {
            labelId = -1;
            while (!open.IsEmpty)
            {
                open.Dequeue(out float queuedScore, out int queuedLabelId);
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

        private static bool TryFindBestRetreatFrontier(List<RetreatSearchLabel> labels, INavigationWorld snapshot, NavigationGoalRegion goalRegion, FlyNavigationParameters parameters, float startRemaining, out int labelId)
        {
            labelId = -1;
            float bestScore = float.PositiveInfinity;
            for (int index = 0; index < labels.Count; index++)
            {
                RetreatSearchLabel label = labels[index];
                if (label.State != RetreatSearchLabelState.Open) continue;
                Vector2 position = NavigationWorldQueries.CellCenter(snapshot, label.Cell);
                if (goalRegion.CompletionDistance(position, parameters.BodySize)
                    >= startRemaining - Tolerance) continue;

                float score = label.RetreatScore;
                if (score < bestScore - Tolerance
                    || Mathf.Abs(score - bestScore) <= Tolerance
                        && (labelId < 0
                            || label.Heuristic < labels[labelId].Heuristic - Tolerance
                            || Mathf.Abs(label.Heuristic - labels[labelId].Heuristic) <= Tolerance
                                && index < labelId))
                {
                    bestScore = score;
                    labelId = index;
                }
            }

            return labelId >= 0;
        }

        private static bool TryFindRetreatConnectorCell(INavigationWorld snapshot, Vector2 position, Vector2 targetCenter, FlyNavigationParameters parameters, out Vector2Int connector)
        {
            connector = default;
            Vector2Int origin = NavigationWorldQueries.WorldToCell(snapshot, position);
            float bestApproach = float.PositiveInfinity;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int y = origin.y - 1; y <= origin.y + 1; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + 1; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (!snapshot.CellBounds.Contains(cell)) continue;
                    Vector2 center = NavigationWorldQueries.CellCenter(snapshot, cell);
                    if (!snapshot.IsCenteredBodyClearAt(center, parameters.BodySize)
                        || !snapshot.IsCenteredBodySegmentClear(position, center, parameters.BodySize, 0.2f)) continue;
                    float approach = RetreatNavigationGeometry.SegmentApproachDistance(position, center, targetCenter);
                    if (parameters.HasApproachLimit && approach > parameters.RemainingApproachDistance + Tolerance) continue;
                    float distanceSquared = (center - position).sqrMagnitude;
                    if (approach > bestApproach + Tolerance
                        || Mathf.Abs(approach - bestApproach) <= Tolerance
                            && distanceSquared >= bestDistanceSquared - Tolerance) continue;
                    bestApproach = approach;
                    bestDistanceSquared = distanceSquared;
                    connector = cell;
                }
            }

            return !float.IsPositiveInfinity(bestApproach);
        }

        /// <summary>Converts a Ground Range lower-center goal into the center anchor used by Fly.</summary>
        private static Vector2 GetGoalCenter(NavigationGoalRegion goalRegion, Vector2 bodySize) => GetGoalCenter(goalRegion, bodySize, goalRegion.Center);

        /// <summary>Converts one resolved lower-center candidate into Fly's center-anchor space.</summary>
        private static Vector2 GetGoalCenter(NavigationGoalRegion goalRegion, Vector2 bodySize, Vector2 candidate) => goalRegion.IsGroundWalk ? candidate + Vector2.up * (bodySize.y * 0.5f) : candidate;

        /// <summary>Checks center-anchored segment samples with explicit cancellation boundaries.</summary>
        private static IEnumerable<bool?> EnumerateCenteredSegmentClear(INavigationWorld snapshot, Vector2 start, Vector2 end, Vector2 bodySize, float maximumSampleSpacing)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / maximumSampleSpacing));
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
            Vector2Int origin = NavigationWorldQueries.WorldToCell(snapshot, position);
            float bestDistanceSquared = float.PositiveInfinity;
            for (int y = origin.y - 1; y <= origin.y + 1; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + 1; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (!snapshot.CellBounds.Contains(cell)) continue;
                    Vector2 center = NavigationWorldQueries.CellCenter(snapshot, cell);
                    float distanceSquared = (center - position).sqrMagnitude;
                    if (distanceSquared + Tolerance >= bestDistanceSquared
                        || !snapshot.IsCenteredBodyClearAt(center, bodySize)
                        || !snapshot.IsCenteredBodySegmentClear(position, center, bodySize, 0.2f)) continue;
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
