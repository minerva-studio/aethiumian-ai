using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Solves collision-validated grounded jumps against one immutable world.
    /// The solver is shareable by planners that use the same world and retains only derived trajectories.
    /// </summary>
    public sealed class GroundJumpSolver
    {
        private const float Tolerance = NavigationConstant.Epsilon;
        private const int MaximumPlannerFlightTicks = 512;
        private readonly JumpTrajectoryCache trajectoryCache;

        /// <summary>Creates a solver with the standard bounded trajectory cache for one world.</summary>
        public GroundJumpSolver(INavigationWorld world) : this(world, JumpTrajectoryCache.DefaultEntryLimit)
        {
        }

        /// <summary>Creates a solver with an internal test-only trajectory cache capacity.</summary>
        internal GroundJumpSolver(INavigationWorld world, int trajectoryCacheEntryLimit)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            trajectoryCache = new JumpTrajectoryCache(trajectoryCacheEntryLimit);
        }

        /// <summary>Gets the immutable geometry queried by this solver for its full lifetime.</summary>
        public INavigationWorld World { get; }

        /// <summary>
        /// Solves a jump or returns a cached deterministic rejection. Cancellation and exceptions never publish a result.
        /// </summary>
        public bool TrySolve(Vector2 start, Vector2 landing, GroundJumpParameters parameters, out JumpTrajectorySolution trajectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!trajectoryCache.TryGet(start, landing, parameters, out JumpTrajectorySolution? cached))
            {
                bool solved = TrySolveUncached(start, landing, parameters, cancellationToken, out trajectory);
                cached = trajectoryCache.Publish(start, landing, parameters, solved ? trajectory : null);
            }
            trajectory = cached.GetValueOrDefault();
            return cached.HasValue;
        }

        private bool TrySolveUncached(Vector2 start, Vector2 landing, GroundJumpParameters parameters, CancellationToken cancellationToken, out JumpTrajectorySolution trajectory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trajectory = default;
            if (!World.TryResolveSupport(AABB.FromLowerCenter(start, parameters.BodySize), NavigationWorldQueries.SupportSnapDistance, out NavigationSupport startSupport)
                || !World.TryResolveSupport(AABB.FromLowerCenter(landing, parameters.BodySize), NavigationWorldQueries.SupportSnapDistance, out NavigationSupport endSupport)
                || Mathf.Abs(endSupport.Position.x - startSupport.Position.x) > parameters.JumpLength + Tolerance)
                return false;

            Vector2 snappedStart = startSupport.Position;
            Vector2 snappedEnd = endSupport.Position;
            float maximumApex = JumpTrajectory.GetMaximumAllowedApexHeight(parameters.JumpHeight);
            float minimumApex = JumpTrajectory.GetDefaultMinimumApexHeight(parameters.JumpHeight);
            if (snappedEnd.y > snappedStart.y + Tolerance)
            {
                minimumApex = Mathf.Max(minimumApex, snappedEnd.y + NavigationConstant.LandingApexClearance - snappedStart.y);
            }
            if (!JumpTrajectory.IsApexHeightAllowed(parameters.JumpHeight, minimumApex) || minimumApex > maximumApex + Tolerance)
                return false;

            JumpTrajectoryInput input = new(snappedStart, snappedEnd, parameters.Gravity, parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight, parameters.SimulationTimeStep);
            for (int attempt = 0; attempt < 64 && minimumApex <= maximumApex + Tolerance; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!JumpTrajectory.TrySolve(input, MaximumPlannerFlightTicks, minimumApex, out JumpTrajectorySolution candidate))
                    return false;

                bool clear = true;
                Vector2 previous = candidate.StartPosition;
                int samples = Mathf.Clamp(
                    Mathf.CeilToInt(candidate.FlightDuration / parameters.SimulationTimeStep)
                    * NavigationConstant.JumpClearanceSubstepsPerPhysicsTick,
                    8,
                    MaximumPlannerFlightTicks * NavigationConstant.JumpClearanceSubstepsPerPhysicsTick);
                for (int index = 1; index <= samples; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Vector2 next = candidate.GetPosition(candidate.FlightDuration * index / samples);
                    AABB body = AABB.FromLowerCenter(previous, parameters.BodySize);
                    if (!World.IsBodyPathClear(body, next - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance))
                    {
                        clear = false;
                        break;
                    }
                    previous = next;
                }

                if (clear && candidate.ApexPosition.y - candidate.StartPosition.y + Tolerance >= minimumApex)
                {
                    trajectory = candidate;
                    return true;
                }
                minimumApex = Mathf.Max(minimumApex, candidate.ApexPosition.y - candidate.StartPosition.y + Tolerance);
            }

            trajectory = default;
            return false;
        }


        /// <summary>
        /// Recreates selected jump trajectories and their OneWay crossing records before a route
        /// crosses the planner boundary. A failed recreation is a planner inconsistency, never an
        /// executable route without the required collision-lease information.
        /// </summary>
        public NavigationRoute PrepareRouteForExecution(NavigationRoute route, GroundJumpParameters parameters, CancellationToken cancellationToken)
        {
            if (!route.HasValue || route.Count == 0) return route;

            List<NavigationRouteSegment> prepared = null;
            for (int index = 0; index < route.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NavigationRouteSegment segment = route[index];
                if (segment is not JumpRouteSegment jump)
                {
                    prepared?.Add(segment);
                    continue;
                }

                if (!GroundJumpGeometry.TryRecreate(this, jump.Start, jump.End, jump.MinimumApexHeight, parameters, cancellationToken, out JumpTrajectorySolution trajectory))
                {
                    throw new InvalidOperationException("Selected jump trajectory could not be recreated with its planning parameters.");
                }

                if (prepared == null)
                {
                    prepared = new List<NavigationRouteSegment>(route.Count);
                    for (int copied = 0; copied < index; copied++) prepared.Add(route[copied]);
                }
                prepared.Add(GroundJumpGeometry.CreateSegment(World, trajectory, parameters.BodySize, cancellationToken));
            }

            return prepared == null ? route : route.WithSegments(prepared);
        }
    }
}
