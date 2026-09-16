using System;
using System.Collections.Generic;
using System.Threading;
using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>One locally validated jump edge.</summary>
    internal sealed class GroundJumpSuccessor
    {
        /// <summary>Gets the stable snapshot candidate ID of the landing support.</summary>
        public int LandingCandidateId { get; }

        /// <summary>Gets the physical support represented by the landing candidate.</summary>
        public NavigationSupport LandingSupport { get; }

        /// <summary>Gets the shared ballistic solution used by planning and execution.</summary>
        public JumpTrajectorySolution Trajectory { get; }

        /// <summary>Creates one validated local jump successor.</summary>
        public GroundJumpSuccessor(Vector2 sourcePosition, int landingCandidateId, NavigationSupport landingSupport,
            JumpTrajectorySolution trajectory)
        {
            SourcePosition = sourcePosition;
            if (landingCandidateId < 0) throw new ArgumentOutOfRangeException(nameof(landingCandidateId));
            LandingCandidateId = landingCandidateId;
            LandingSupport = landingSupport;
            Trajectory = trajectory ?? throw new ArgumentNullException(nameof(trajectory));
        }

        /// <summary>Gets the exact search-node position that owns this transition.</summary>
        public Vector2 SourcePosition { get; }

        /// <summary>
        /// Creates the search-only jump segment represented by this successor.
        /// The transition source belongs to the search node; the trajectory may use
        /// a support-resolved start internally.
        /// </summary>
        public JumpRouteSegment CreateSegment()
            => new(SourcePosition, Trajectory.LandingPosition,
                Mathf.Max(0f, Trajectory.ApexPosition.y - Trajectory.StartPosition.y));
    }

    /// <summary>Generates bounded jump successors from one grounded search node.</summary>
    internal static class GroundJumpSuccessorEnumerator
    {
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// Enumerates the local jump envelope. Null yields represent bounded scheduler work;
        /// non-null values are complete, collision-validated jump edges.
        /// </summary>
        public static IEnumerable<GroundJumpSuccessor> Enumerate(GroundJumpSolver jumpSolver, Vector2 start,
            NavigationSupport support, NavigationGoalRequest goal, GroundJumpParameters parameters,
            NavigationPlanningDiagnostics diagnostics = null,
            bool excludeGroundAdjacent = false, int launchCandidateId = -1)
        {
            if (jumpSolver == null) throw new ArgumentNullException(nameof(jumpSolver));
            INavigationWorld world = jumpSolver.World;
            // A profile that cannot produce any trajectory has no jump edges. Asking the trajectory
            // owner keeps a Walk profile authored without jump capability planable instead of letting
            // the search fail on malformed trajectory input.
            if (!JumpTrajectory.CanProduceTrajectory(parameters.JumpHeight,
                parameters.Gravity.y * parameters.GravityScale))
                yield break;

            float maximumApexHeight = JumpTrajectory.GetMaximumAllowedApexHeight(parameters.JumpHeight);
            Vector2Int supportCell = NavigationWorldQueries.WorldToCell(world, support.Position);
            IReadOnlyList<NavigationSupportCandidate> landingSupports;
            int horizontalCells = Mathf.CeilToInt(parameters.JumpLength / world.CellSize);
            int minX = Mathf.Max(world.CellBounds.xMin, supportCell.x - horizontalCells);
            int maxX = Mathf.Min(world.CellBounds.xMax - 1, supportCell.x + horizontalCells);
            float minimumY = world.Origin.y + world.CellBounds.yMin * world.CellSize;
            float maximumY = Mathf.Min(world.Origin.y + world.CellBounds.yMax * world.CellSize,
                start.y + maximumApexHeight + Tolerance);
            Rect landingBounds = new(
                world.Origin.x + minX * world.CellSize,
                minimumY,
                (maxX - minX + 1) * world.CellSize,
                Mathf.Max(0f, maximumY - minimumY));
            IReadOnlyList<NavigationSupportCandidate> supportCandidates =
                world.GetSupportCandidates(landingBounds, parameters.BodySize);
            List<NavigationSupportCandidate> builtLandings = new();
            for (int index = 0; index < supportCandidates.Count; index++)
            {
                NavigationSupportCandidate landingCandidate = supportCandidates[index];
                NavigationSupport landingSupport = landingCandidate.Support;
                diagnostics?.RecordJumpCandidateGenerated();
                if (landingCandidate.Id == launchCandidateId && launchCandidateId >= 0
                    || landingSupport.Surface == support.Surface && landingSupport.Position == support.Position)
                    diagnostics?.RecordJumpCandidatePruned();
                else
                    AddUnique(builtLandings, landingCandidate);
                yield return null;
            }

            landingSupports = builtLandings;

            JumpCandidateHeap candidates = new();
            float startDistance = goal.DistanceToLowerCenterBody(start, parameters.BodySize);
            for (int i = 0; i < landingSupports.Count; i++)
            {
                NavigationSupportCandidate landingCandidate = landingSupports[i];
                NavigationSupport landingSupport = landingCandidate.Support;
                Vector2Int landingCell = NavigationWorldQueries.WorldToCell(world, landingSupport.Position);
                if (excludeGroundAdjacent && landingCell.y == supportCell.y
                    && Mathf.Abs(landingCell.x - supportCell.x) == 1)
                {
                    diagnostics?.RecordJumpCandidatePruned();
                    yield return null;
                    continue;
                }

                Vector2 landing = landingSupport.Position;
                if (Mathf.Abs(landing.x - start.x) > parameters.JumpLength + Tolerance
                    || landing.y > start.y + maximumApexHeight + Tolerance)
                {
                    diagnostics?.RecordJumpCandidatePruned();
                    yield return null;
                    continue;
                }
                candidates.Enqueue(new JumpCandidateDescriptor(landingCandidate.Id, landingCell, landingSupport, landing,
                    CouldTrajectoryEnterGoal(start, landing, goal, parameters, maximumApexHeight),
                    startDistance - goal.DistanceToLowerCenterBody(landing, parameters.BodySize),
                    Mathf.Sign(landing.x - start.x) == Mathf.Sign(goal.Center.x - start.x),
                    Vector2.Distance(start, landing)));
                yield return null;
            }

            while (!candidates.IsEmpty)
            {
                JumpCandidateDescriptor candidate = candidates.Dequeue();
                yield return null;
                JumpTrajectorySolution solution;
                if (!jumpSolver.TrySolve(start, candidate.Landing, parameters, out solution))
                {
                    diagnostics?.RecordJumpCandidatePruned();
                    yield return null;
                    continue;
                }
                diagnostics?.RecordJumpCandidateValidated();
                yield return new GroundJumpSuccessor(start, candidate.CandidateId, candidate.Support, solution);
            }
        }

        /// <summary>
        /// Recreates selected jump trajectories and their OneWay crossing records before a route
        /// crosses the planner boundary. A failed recreation is a planner inconsistency, never an
        /// executable route without the required collision-lease information.
        /// </summary>
        internal static NavigationRoute PrepareRouteForExecution(GroundJumpSolver jumpSolver,
            NavigationRoute route,
            GroundJumpParameters parameters, CancellationToken cancellationToken)
        {
            if (jumpSolver == null) throw new ArgumentNullException(nameof(jumpSolver));
            INavigationWorld world = jumpSolver.World;
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (route == null || route.Count == 0) return route;

            List<NavigationRouteSegment> prepared = null;
            for (int index = 0; index < route.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NavigationRouteSegment segment = route.Segments[index];
                if (segment is not JumpRouteSegment jump)
                {
                    prepared?.Add(segment);
                    continue;
                }

                if (!GroundJumpGeometry.TryRecreate(jumpSolver, jump.LaunchSupport, jump.PlannedLanding,
                        jump.MinimumApexHeight, parameters, cancellationToken, out JumpTrajectorySolution trajectory))
                {
                    throw new InvalidOperationException(
                        "Selected jump trajectory could not be recreated with its planning parameters.");
                }

                if (prepared == null)
                {
                    prepared = new List<NavigationRouteSegment>(route.Count);
                    for (int copied = 0; copied < index; copied++) prepared.Add(route.Segments[copied]);
                }
                prepared.Add(GroundJumpGeometry.CreateSegment(world, trajectory, parameters.BodySize,
                    parameters.SupportSnapDistance, cancellationToken));
            }

            return prepared == null ? route : route.WithSegments(prepared);
        }

        private static bool CouldTrajectoryEnterGoal(Vector2 start, Vector2 landing,
            NavigationGoalRequest goal, GroundJumpParameters parameters, float maximumApexHeight)
        {
            float minX = Mathf.Min(start.x, landing.x) - parameters.BodySize.x * 0.5f;
            float maxX = Mathf.Max(start.x, landing.x) + parameters.BodySize.x * 0.5f;
            float minY = Mathf.Min(start.y, landing.y);
            float maxY = Mathf.Max(start.y, landing.y) + maximumApexHeight + parameters.BodySize.y;
            Bounds envelope = new(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f),
                new Vector3(maxX - minX, maxY - minY, 0f));
            Bounds reachable = goal.IsGroundWalk
                ? goal.GetLowerCenterAcceptanceBounds(parameters.BodySize.x)
                : goal.TargetBounds;
            return envelope.Intersects(reachable);
        }

        private static void AddUnique(List<NavigationSupportCandidate> supports, NavigationSupportCandidate candidate)
        {
            for (int index = 0; index < supports.Count; index++)
                if (supports[index].Id == candidate.Id)
                    return;
            supports.Add(candidate);
        }
    }

    internal readonly struct JumpCandidateDescriptor
    {
        public readonly int CandidateId;
        public readonly Vector2Int Cell;
        public readonly NavigationSupport Support;
        public readonly Vector2 Landing;
        private readonly bool couldEnterGoal;
        private readonly float theoreticalImprovement;
        private readonly bool targetDirection;
        private readonly float estimatedCost;

        /// <summary>Creates one cheaply scored landing descriptor before trajectory validation.</summary>
        public JumpCandidateDescriptor(int candidateId, Vector2Int cell, NavigationSupport support, Vector2 landing, bool couldEnterGoal,
            float theoreticalImprovement, bool targetDirection, float estimatedCost)
        {
            CandidateId = candidateId;
            Cell = cell;
            Support = support;
            Landing = landing;
            this.couldEnterGoal = couldEnterGoal;
            this.theoreticalImprovement = theoreticalImprovement;
            this.targetDirection = targetDirection;
            this.estimatedCost = estimatedCost;
        }

        /// <summary>Orders descriptors without deleting temporarily regressive candidates.</summary>
        public static int Compare(JumpCandidateDescriptor left, JumpCandidateDescriptor right)
        {
            int comparison = right.couldEnterGoal.CompareTo(left.couldEnterGoal);
            if (comparison != 0) return comparison;
            comparison = right.theoreticalImprovement.CompareTo(left.theoreticalImprovement);
            if (comparison != 0) return comparison;
            comparison = right.targetDirection.CompareTo(left.targetDirection);
            if (comparison != 0) return comparison;
            comparison = left.estimatedCost.CompareTo(right.estimatedCost);
            if (comparison != 0) return comparison;
            comparison = left.CandidateId.CompareTo(right.CandidateId);
            if (comparison != 0) return comparison;
            comparison = left.Cell.y.CompareTo(right.Cell.y);
            return comparison != 0 ? comparison : left.Cell.x.CompareTo(right.Cell.x);
        }
    }

    /// <summary>Incrementally orders cheap jump descriptors without an all-at-once sort.</summary>
    internal sealed class JumpCandidateHeap
    {
        private readonly List<JumpCandidateDescriptor> entries = new();

        /// <summary>Gets whether no descriptor remains.</summary>
        public bool IsEmpty => entries.Count == 0;

        /// <summary>Adds one descriptor in logarithmic work.</summary>
        public void Enqueue(JumpCandidateDescriptor descriptor)
        {
            entries.Add(descriptor);
            int index = entries.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (JumpCandidateDescriptor.Compare(entries[index], entries[parent]) >= 0) break;
                (entries[index], entries[parent]) = (entries[parent], entries[index]);
                index = parent;
            }
        }

        /// <summary>Removes the highest-priority descriptor in logarithmic work.</summary>
        public JumpCandidateDescriptor Dequeue()
        {
            if (entries.Count == 0) throw new InvalidOperationException("The jump candidate heap is empty.");
            JumpCandidateDescriptor result = entries[0];
            int last = entries.Count - 1;
            entries[0] = entries[last];
            entries.RemoveAt(last);
            for (int index = 0; ;)
            {
                int left = index * 2 + 1;
                if (left >= entries.Count) break;
                int right = left + 1;
                int child = right < entries.Count
                    && JumpCandidateDescriptor.Compare(entries[right], entries[left]) < 0 ? right : left;
                if (JumpCandidateDescriptor.Compare(entries[child], entries[index]) >= 0) break;
                (entries[index], entries[child]) = (entries[child], entries[index]);
                index = child;
            }

            return result;
        }
    }

}
