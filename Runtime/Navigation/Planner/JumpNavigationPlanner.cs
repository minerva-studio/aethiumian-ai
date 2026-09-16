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
        public override NavigationPlanResult Plan(Vector2 start, NavigationGoalRequest goal, JumpNavigationParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null)
        {
            ValidatePlanInputs(start, cancellationToken);
            parameters = PrepareParameters(parameters);
            ValidateParameters(parameters);

            NavigationPlanResult result;
            if (!World.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport startSupport))
            {
                result = NavigationPlanResult.NoResult;
            }
            else if (!CanGenerateJumpEdges(parameters))
            {
                result = NavigationPlanResult.NoResult;
            }
            else
            {
                Vector2 startCenter = resolvedStart + Vector2.up * (parameters.BodySize.y * 0.5f);
                if (World.IsGoalComplete(goal, startCenter, parameters.BodySize))
                {
                    var complete = NavigationRoute.Complete(resolvedStart, goal, World, resolvedStart, Array.Empty<NavigationRouteSegment>());
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

            return result.Route == null ? result : result.WithRoute(PrepareRouteForExecution(result.Route, parameters, cancellationToken));
        }

        /// <summary>Expands only the actual launch support and returns one validated landing action.</summary>
        public override NavigationPlanResult PlanSingleStep(Vector2 start, NavigationGoalRequest goal, JumpNavigationParameters parameters, CancellationToken cancellationToken = default)
        {
            ValidatePlanInputs(start, cancellationToken);
            parameters = PrepareParameters(parameters);
            ValidateParameters(parameters);
            if (!CanGenerateJumpEdges(parameters) || !World.TryResolveGroundSupport(start, parameters.BodySize,
                parameters.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport support)) return NavigationPlanResult.NoResult;
            Vector2 center = resolvedStart + Vector2.up * (parameters.BodySize.y * 0.5f);
            if (World.IsGoalComplete(goal, center, parameters.BodySize))
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(resolvedStart, goal, World, resolvedStart, Array.Empty<NavigationRouteSegment>()));
            var node = new NavigationSearchNode(NavigationNodeIdentity.Jump(-1), resolvedStart, support, 0f);
            NavigationTransition? best = null;
            float distance = goal.GuidanceDistance(center, parameters.BodySize);
            int work = 0;
            bool budgetReached = false;
            foreach (NavigationTransitionWork candidate in EnumerateSharedTransitions(node, goal, parameters, null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++work > MaxExpandedNodes) { budgetReached = true; break; }
                if (!candidate.HasTransition) continue;
                NavigationTransition edge = candidate.Transition;
                Vector2 landingCenter = edge.DestinationPosition + Vector2.up * (parameters.BodySize.y * 0.5f);
                float nextDistance = goal.GuidanceDistance(landingCenter, parameters.BodySize);
                if (!edge.CompletesGoal && !IsStrictlyLess(nextDistance, distance)) continue;
                best = edge;
                distance = nextDistance;
                if (edge.CompletesGoal) break;
            }
            if (!best.HasValue) return budgetReached ? NavigationPlanResult.BudgetReached() : NavigationPlanResult.NoResult;
            NavigationTransition selected = best.Value;
            NavigationRoute route = NavigationRoute.Create(resolvedStart, goal, World, selected.DestinationPosition,
                new[] { selected.Segment }, selected.CompletesGoal);
            route = PrepareRouteForExecution(route, parameters, cancellationToken);
            return NavigationPlanResult.ResultProduced(route);
        }

        private IEnumerable<NavigationTransitionWork> EnumerateSharedTransitions(NavigationSearchNode node, NavigationGoalRequest goal, JumpNavigationParameters parameters, NavigationPlanningDiagnostics diagnostics)
        {
            GroundJumpParameters jumpParameters = parameters.GetGroundJumpParameters();

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
                Vector2 landingCenter = jump.Trajectory.LandingPosition + Vector2.up * (parameters.BodySize.y * 0.5f);
                bool completesGoal = World.IsGoalComplete(goal, landingCenter, parameters.BodySize);
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

            Vector2 center = lowerCenter + Vector2.up * (bodySize.y * 0.5f);
            float completionDistance = World.GetGoalCompletionDistance(goal, center, bodySize);
            return Mathf.Max(0f, completionDistance - goal.CompletionTolerance);
        }

        /// <summary>Targets the goal within jump range instead of landing on its completion boundary.</summary>
        private static bool TryCreateDirectGoalSuccessor(GroundJumpSolver jumpSolver, Vector2 start, NavigationGoalRequest goal, GroundJumpParameters parameters, out Successor successor)
        {
            successor = default;
            // A boundary-only landing turns ordinary physics contact error into another jump.
            // Aim toward the goal itself; the normal search handles unsupported destinations.
            Vector2 landing = new(start.x + Mathf.Clamp(goal.Center.x - start.x, -parameters.JumpLength, parameters.JumpLength), start.y);
            if (Mathf.Abs(landing.x - start.x) <= Tolerance
                || !jumpSolver.World.IsGoalComplete(goal, landing + Vector2.up * (parameters.BodySize.y * 0.5f), parameters.BodySize))
                return false;
            if (!jumpSolver.TrySolve(start, landing, parameters, out JumpTrajectorySolution trajectory))
                return false;

            Vector2 landingCenter = trajectory.LandingPosition + Vector2.up * (parameters.BodySize.y * 0.5f);
            if (!jumpSolver.World.IsGoalComplete(goal, landingCenter, parameters.BodySize)) return false;

            JumpRouteSegment segment = new(start, trajectory.LandingPosition,
                Mathf.Max(0f, trajectory.ApexPosition.y - trajectory.StartPosition.y));
            successor = new Successor(trajectory.LandingPosition, segment, Vector2.Distance(start, trajectory.LandingPosition) + trajectory.FlightDuration);
            return true;
        }

        /// <summary>Checks whether the configured profile produces a usable vertical trajectory.</summary>
        private static bool CanGenerateJumpEdges(JumpNavigationParameters parameters)
            => Mathf.Abs(parameters.Gravity.x) <= 0.001f
                && Mathf.Abs(parameters.Gravity.y * parameters.GravityScale) > 0.001f
                && parameters.JumpHeight > Tolerance
                && parameters.JumpLength >= 0f;

        private static void ValidateParameters(JumpNavigationParameters profile)
        {
            Validate.PositiveVector(profile.BodySize, nameof(profile));
            Validate.NonNegativeFinite(profile.GravityScale, nameof(profile));
            Validate.NonNegativeFinite(profile.LinearDamping, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpHeight, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpLength, nameof(profile));
            Validate.PositiveFinite(profile.SimulationTimeStep, nameof(profile));
            Validate.Finite(profile.Gravity, nameof(profile));
        }

        private static JumpNavigationParameters CaptureSupportSnapDistance(JumpNavigationParameters parameters)
            => parameters.SupportSnapDistance > 0f
                ? parameters
                : parameters.WithSupportSnapDistance(NavigationWorldQueries.SupportSnapDistance);

        private static JumpNavigationParameters CaptureGroundContactTolerance(JumpNavigationParameters parameters)
            => parameters.GroundContactTolerance > 0f
                ? parameters
                : parameters.WithGroundContactTolerance(NavigationWorldQueries.GeometryEpsilon);

        private NavigationRoute PrepareRouteForExecution(NavigationRoute route, JumpNavigationParameters parameters, CancellationToken cancellationToken)
            => GroundJumpSuccessorEnumerator.PrepareRouteForExecution(jumpSolver, route, parameters.GetGroundJumpParameters(), cancellationToken);

        private static JumpNavigationParameters PrepareParameters(JumpNavigationParameters parameters) => CaptureGroundContactTolerance(CaptureSupportSnapDistance(parameters));

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
