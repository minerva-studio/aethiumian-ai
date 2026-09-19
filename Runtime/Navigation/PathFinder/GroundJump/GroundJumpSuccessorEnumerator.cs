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
        private const float Tolerance = NavigationConstant.Epsilon;

        /// <summary>
        /// Enumerates the local jump envelope. Null yields represent bounded scheduler work;
        /// non-null values are complete, collision-validated jump edges.
        /// </summary>
        public static IEnumerable<GroundJumpSuccessor> Enumerate(GroundJumpSolver jumpSolver,
            Vector2 start,
            NavigationSupport support,
            NavigationGoalRequest goal,
            GroundJumpParameters parameters,
            NavigationPlanningDiagnostics diagnostics = null,
            bool excludeGroundAdjacent = false,
            int launchCandidateId = -1)
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
            IReadOnlyList<NavigationSupportCandidate> landingSupports;
            AABB worldBounds = world.WorldBounds;
            float minimumY = worldBounds.MinY;
            float maximumY = Mathf.Min(worldBounds.MaxY, start.y + maximumApexHeight + Tolerance);
            float horizontalReach = Mathf.Max(parameters.JumpLength, Tolerance);
            AABB landingBounds = new(
                new Vector2(Mathf.Max(worldBounds.MinX, start.x - horizontalReach), minimumY),
                new Vector2(Mathf.Min(worldBounds.MaxX, start.x + horizontalReach + Tolerance), Mathf.Max(minimumY, maximumY)));
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
            AABB startBody = AABB.FromLowerCenter(start, parameters.BodySize);
            float startDistance = goal.DistanceToLowerCenterBody(startBody);
            for (int i = 0; i < landingSupports.Count; i++)
            {
                NavigationSupportCandidate landingCandidate = landingSupports[i];
                NavigationSupport landingSupport = landingCandidate.Support;
                if (excludeGroundAdjacent
                    && Mathf.Abs(landingSupport.Position.y - support.Position.y) <= GroundTraversalEndpointPolicy.VerticalSupportTolerance
                    && Mathf.Abs(landingSupport.Position.x - support.Position.x) <= NavigationConstant.AdjacentSupportReach)
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
                candidates.Enqueue(new JumpCandidateDescriptor(landingCandidate.Id, landingSupport, landing,
                    CouldTrajectoryEnterGoal(start, landing, goal, parameters, maximumApexHeight),
                    startDistance - goal.DistanceToLowerCenterBody(AABB.FromLowerCenter(landing, parameters.BodySize)),
                    Mathf.Sign(landing.x - start.x) == Mathf.Sign(goal.TargetBounds.CenterX - start.x),
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

        private static bool CouldTrajectoryEnterGoal(Vector2 start, Vector2 landing, NavigationGoalRequest goal, GroundJumpParameters parameters, float maximumApexHeight)
        {
            float minX = Mathf.Min(start.x, landing.x) - parameters.BodySize.x * 0.5f;
            float maxX = Mathf.Max(start.x, landing.x) + parameters.BodySize.x * 0.5f;
            float minY = Mathf.Min(start.y, landing.y);
            float maxY = Mathf.Max(start.y, landing.y) + maximumApexHeight + parameters.BodySize.y;
            AABB envelope = new(new Vector2(minX, minY), new Vector2(maxX, maxY));
            AABB reachable = goal.IsGroundWalk
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
        public readonly NavigationSupport Support;
        public readonly Vector2 Landing;
        private readonly bool couldEnterGoal;
        private readonly float theoreticalImprovement;
        private readonly bool targetDirection;
        private readonly float estimatedCost;

        /// <summary>
        /// Creates one cheaply scored landing descriptor before trajectory validation.
        /// </summary>
        public JumpCandidateDescriptor(int candidateId, NavigationSupport support, Vector2 landing, bool couldEnterGoal, float theoreticalImprovement, bool targetDirection, float estimatedCost)
        {
            CandidateId = candidateId;
            Support = support;
            Landing = landing;
            this.couldEnterGoal = couldEnterGoal;
            this.theoreticalImprovement = theoreticalImprovement;
            this.targetDirection = targetDirection;
            this.estimatedCost = estimatedCost;
        }

        /// <summary>
        /// Orders descriptors without deleting temporarily regressive candidates.
        /// </summary>
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
            return left.CandidateId.CompareTo(right.CandidateId);
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
