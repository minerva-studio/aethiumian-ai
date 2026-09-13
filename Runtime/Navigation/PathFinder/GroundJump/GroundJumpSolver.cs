using System;
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
        private const float Tolerance = 0.0001f;
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
            if (trajectoryCache.TryGet(start, landing, parameters, out trajectory)) return trajectory != null;

            TrySolveUncached(start, landing, parameters, cancellationToken, out trajectory);
            trajectory = trajectoryCache.Publish(start, landing, parameters, trajectory);
            return trajectory != null;
        }

        private void TrySolveUncached(Vector2 start, Vector2 landing, GroundJumpParameters parameters, CancellationToken cancellationToken, out JumpTrajectorySolution trajectory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trajectory = null;
            if (!World.TryResolveSupport(start, parameters.BodySize, parameters.SupportSnapDistance,
                    out NavigationSupport startSupport)
                || !World.TryResolveSupport(landing, parameters.BodySize, parameters.SupportSnapDistance,
                    out NavigationSupport endSupport)
                || Mathf.Abs(endSupport.Position.x - startSupport.Position.x) > parameters.JumpLength + Tolerance)
                return;

            Vector2 snappedStart = startSupport.Position;
            Vector2 snappedEnd = endSupport.Position;
            float maximumApex = JumpTrajectory.GetMaximumAllowedApexHeight(parameters.JumpHeight);
            float minimumApex = snappedEnd.y <= snappedStart.y + Tolerance
                ? JumpTrajectory.GetDefaultMinimumApexHeight(parameters.JumpHeight)
                : Mathf.Max(JumpTrajectory.GetDefaultMinimumApexHeight(parameters.JumpHeight),
                    snappedEnd.y + World.CellSize - snappedStart.y);
            if (!JumpTrajectory.IsApexHeightAllowed(parameters.JumpHeight, minimumApex)
                || minimumApex > maximumApex + Tolerance) return;

            JumpTrajectoryInput input = new(snappedStart, snappedEnd, parameters.Gravity,
                parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight,
                parameters.SimulationTimeStep);
            for (int attempt = 0; attempt < 64 && minimumApex <= maximumApex + Tolerance; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!JumpTrajectory.TrySolve(input, MaximumPlannerFlightTicks, minimumApex, out trajectory))
                    return;

                bool clear = true;
                Vector2 previous = trajectory.StartPosition;
                int samples = Mathf.Clamp(Mathf.CeilToInt(trajectory.FlightDuration / 0.02f), 8, 256);
                for (int index = 1; index <= samples; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Vector2 next = trajectory.GetPosition(trajectory.FlightDuration * index / samples);
                    Rect body = new(previous.x - parameters.BodySize.x * 0.5f, previous.y,
                        parameters.BodySize.x, parameters.BodySize.y);
                    if (!World.IsBodyPathClear(body, next - previous, parameters.GroundContactTolerance))
                    {
                        clear = false;
                        break;
                    }
                    previous = next;
                }

                if (clear && trajectory.ApexPosition.y - trajectory.StartPosition.y + Tolerance >= minimumApex)
                    return;
                minimumApex = Mathf.Max(minimumApex,
                    trajectory.ApexPosition.y - trajectory.StartPosition.y + Tolerance);
            }

            trajectory = null;
            return;
        }
    }
}
