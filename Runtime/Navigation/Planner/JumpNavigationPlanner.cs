using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Plans bounded repeated ballistic jumps with locally generated landing successors.</summary>
    public sealed class JumpNavigationPlanner : NavigationPlanner<JumpNavigationParameters>
    {
        private readonly GroundJumpSolver jumpSolver;

        /// <summary>
        /// Creates a planner that shares one immutable world's jump solver.
        /// </summary>
        public JumpNavigationPlanner(INavigationWorld world, int maxExpandedNodes, GroundJumpSolver jumpSolver) : base(world, maxExpandedNodes)
        {
            this.jumpSolver = jumpSolver;
        }

        /// <summary>
        /// Runs jump planning through the shared action-graph search.
        /// </summary>
        public override NavigationPlanResult Plan(AABB body, NavigationGoalRequest goal, JumpNavigationParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null)
        {
            ValidatePlanInputs(body, cancellationToken);
            ValidateParameters(parameters);

            NavigationPlanResult result;
            if (!World.TryResolveGroundSupport(AABB.FromLowerCenter(body.LowerCenter, parameters.BodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport startSupport))
            {
                result = NavigationPlanResult.NoResult;
            }
            else
            {
                AABB startBody = AABB.FromLowerCenter(resolvedStart, parameters.BodySize);
                if (World.IsGoalComplete(goal, startBody))
                {
                    var complete = NavigationRoute.Empty(resolvedStart, goal, NavigationRouteCoordinateFrame.GroundAnchor, true);
                    result = NavigationPlanResult.ResultProduced(complete);
                }
                else
                {
                    NavigationSearchRequest request = new(World, resolvedStart, startSupport, goal,
                        parameters.BodySize, NavigationActions.Jump, MaxExpandedNodes,
                        NavigationNodeIdentity.Jump(-1),
                        node => EnumerateSharedTransitions(node, goal, parameters, diagnostics),
                        position => EvaluateGoalHeuristic(position, goal, parameters.BodySize));
                    result = RunSearch(request, diagnostics, cancellationToken);
                }
            }

            if (result.Route == null)
            {
                return result;
            }
            else
            {
                return result.WithRoute(jumpSolver.PrepareRouteForExecution(result.Route, parameters.GetJumpParameters(), cancellationToken));
            }
        }

        /// <summary>Expands only the actual launch support and returns one validated landing action.</summary>
        public override NavigationPlanResult PlanSingleStep(AABB body, NavigationGoalRequest goal, JumpNavigationParameters parameters, CancellationToken cancellationToken = default)
        {
            ValidatePlanInputs(body, cancellationToken);
            ValidateParameters(parameters);
            if (!World.TryResolveGroundSupport(AABB.FromLowerCenter(body.LowerCenter, parameters.BodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport support))
            {
                return NavigationPlanResult.NoResult;
            }

            AABB startBody = AABB.FromLowerCenter(resolvedStart, parameters.BodySize);
            if (World.IsGoalComplete(goal, startBody))
            {
                return NavigationPlanResult.ResultProduced(NavigationRoute.Empty(resolvedStart, goal, NavigationRouteCoordinateFrame.GroundAnchor, true));
            }

            var node = new NavigationSearchNode(NavigationNodeIdentity.Jump(-1), resolvedStart, support, 0f);
            NavigationTransition? best = null;
            float distance = goal.GuidanceDistance(startBody);
            int work = 0;
            bool budgetReached = false;
            foreach (NavigationTransitionWork candidate in EnumerateSharedTransitions(node, goal, parameters, null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++work > MaxExpandedNodes) { budgetReached = true; break; }
                if (!candidate.HasTransition) continue;
                NavigationTransition edge = candidate.Transition;
                AABB landingBody = AABB.FromLowerCenter(edge.DestinationPosition, parameters.BodySize);
                float nextDistance = goal.GuidanceDistance(landingBody);
                if (!edge.CompletesGoal && !IsStrictlyLess(nextDistance, distance)) continue;
                best = edge;
                distance = nextDistance;
                if (edge.CompletesGoal) break;
            }
            if (!best.HasValue) return budgetReached ? NavigationPlanResult.BudgetReached() : NavigationPlanResult.NoResult;
            NavigationTransition selected = best.Value;
            NavigationRoute route = NavigationRoute.Create(goal, new[] { selected.Segment }, selected.CompletesGoal);

            route = jumpSolver.PrepareRouteForExecution(route, parameters.GetJumpParameters(), cancellationToken);
            return NavigationPlanResult.ResultProduced(route);
        }

        private IEnumerable<NavigationTransitionWork> EnumerateSharedTransitions(NavigationSearchNode node, NavigationGoalRequest goal, JumpNavigationParameters parameters, NavigationPlanningDiagnostics diagnostics)
        {
            GroundJumpParameters jumpParameters = parameters.GetJumpParameters();

            if (TryCreateDirectGoalSuccessor(jumpSolver, node.Position, goal, jumpParameters, out Successor nearestGoal))
            {
                diagnostics?.RecordTerminalCandidate();
                yield return NavigationTransitionWork.Edge(NavigationTransition.CompletedJump(nearestGoal.Position, nearestGoal.Step, nearestGoal.Cost));
            }

            foreach (GroundJumpSuccessor jump in GroundJumpSuccessorEnumerator.Enumerate(jumpSolver, node.Position,
                node.Support, goal, jumpParameters, diagnostics, false, node.Identity.CandidateId))
            {
                yield return NavigationTransitionWork.WorkUnit;
                if (jump == null) continue;
                AABB landingBody = AABB.FromLowerCenter(jump.Trajectory.LandingPosition, parameters.BodySize);
                bool completesGoal = World.IsGoalComplete(goal, landingBody);
                if (completesGoal)
                    diagnostics?.RecordTerminalCandidate();
                yield return NavigationTransitionWork.Edge(NavigationTransition.JumpLanding(
                    NavigationNodeIdentity.Jump(jump.LandingCandidateId), jump.Trajectory.LandingPosition,
                    jump.LandingSupport, jump.CreateSegment(),
                    Vector2.Distance(node.Position, jump.Trajectory.LandingPosition) + jump.Trajectory.FlightDuration, completesGoal));
            }
        }

        /// <summary>
        /// Returns an admissible Euclidean lower bound for ordinary approach goals. Goals whose
        /// metric or retreat semantics do not provide that bound retain zero guidance.
        /// </summary>
        private float EvaluateGoalHeuristic(Vector2 lowerCenter, NavigationGoalRequest goal, Vector2 bodySize)
        {
            if (goal.IsRetreat || goal.DistanceMetric != DistanceMetric.Euclidean) return 0f;

            AABB body = AABB.FromLowerCenter(lowerCenter, bodySize);
            float completionDistance = World.GetGoalCompletionDistance(goal, body);
            return Mathf.Max(0f, completionDistance - goal.CompletionTolerance);
        }

        /// <summary>Targets the goal within jump range instead of landing on its completion boundary.</summary>
        private static bool TryCreateDirectGoalSuccessor(GroundJumpSolver jumpSolver, Vector2 start, NavigationGoalRequest goal, GroundJumpParameters parameters, out Successor successor)
        {
            successor = default;
            // A boundary-only landing turns ordinary physics contact error into another jump.
            // Aim toward the goal itself; the normal search handles unsupported destinations.
            Vector2 landing = new(start.x + Mathf.Clamp(goal.TargetBounds.CenterX - start.x, -parameters.JumpLength, parameters.JumpLength), start.y);
            if (Mathf.Abs(landing.x - start.x) <= Tolerance
                || !jumpSolver.World.IsGoalComplete(goal, AABB.FromLowerCenter(landing, parameters.BodySize)))
                return false;
            if (!jumpSolver.TrySolve(start, landing, parameters, out JumpTrajectorySolution trajectory))
                return false;

            if (!jumpSolver.World.IsGoalComplete(goal, AABB.FromLowerCenter(trajectory.LandingPosition, parameters.BodySize))) return false;

            JumpRouteSegment segment = new(start, trajectory.LandingPosition,
                Mathf.Max(0f, trajectory.ApexPosition.y - trajectory.StartPosition.y));
            successor = new Successor(trajectory.LandingPosition, segment, Vector2.Distance(start, trajectory.LandingPosition) + trajectory.FlightDuration);
            return true;
        }

        private static void ValidateParameters(JumpNavigationParameters profile)
        {
            Validate.PositiveVector(profile.BodySize, nameof(profile));
            Validate.NonNegativeFinite(profile.GravityScale, nameof(profile));
            Validate.NonNegativeFinite(profile.LinearDamping, nameof(profile));
            Validate.PositiveFinite(profile.JumpHeight, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpLength, nameof(profile));
            Validate.PositiveFinite(profile.SimulationTimeStep, nameof(profile));
            Validate.Finite(profile.Gravity, nameof(profile));
        }

        private readonly struct Successor
        {
            public readonly Vector2 Position;
            public readonly NavigationRouteSegment Step;
            public readonly float Cost;

            public Successor(Vector2 position, NavigationRouteSegment step, float cost)
            {
                Position = position;
                Step = step;
                Cost = cost;
            }
        }
    }
}
