using System;
using System.Collections.Generic;
using System.Threading;
using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Builds execution-ready grounded-jump route segments without owning search policy.</summary>
    public static class GroundJumpGeometry
    {
        private const float Tolerance = NavigationConstant.Epsilon;

        /// <summary>
        /// Re-solves one selected jump and confirms the planned apex is still the exact
        /// collision-validated solution for the immutable world used to publish the route.
        /// </summary>
        internal static bool TryRecreate(GroundJumpSolver jumpSolver, Vector2 start, Vector2 landing,
            float plannedApexHeight, GroundJumpParameters parameters, CancellationToken cancellationToken,
            out JumpTrajectorySolution trajectory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trajectory = default;
            if (!NavigationNumeric.IsFinite(plannedApexHeight)
                || plannedApexHeight < 0f || !jumpSolver.TrySolve(start, landing, parameters,
                    out trajectory, cancellationToken))
                return false;

            cancellationToken.ThrowIfCancellationRequested();
            float recreatedApexHeight = Mathf.Max(0f, trajectory.ApexPosition.y - trajectory.StartPosition.y);
            return Mathf.Abs(recreatedApexHeight - plannedApexHeight) <= Tolerance;
        }

        /// <summary>Creates one route segment and derives its directed OneWay crossings.</summary>
        public static JumpRouteSegment CreateSegment(INavigationWorld world,
            JumpTrajectorySolution trajectory, Vector2 bodySize, float supportSnapDistance)
            => CreateSegment(world, trajectory, bodySize, supportSnapDistance, CancellationToken.None);

        /// <summary>Creates one execution-ready route segment while allowing planning cancellation.</summary>
        public static JumpRouteSegment CreateSegment(INavigationWorld world,
            JumpTrajectorySolution trajectory, Vector2 bodySize, float supportSnapDistance,
            CancellationToken cancellationToken)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (trajectory == null) throw new ArgumentNullException(nameof(trajectory));
            if (!NavigationNumeric.IsFinite(supportSnapDistance)
                || supportSnapDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(supportSnapDistance));

            IReadOnlyList<JumpSurfaceCrossing> crossings = CreateSurfaceCrossings(world, trajectory, bodySize,
                cancellationToken);
            return new JumpRouteSegment(trajectory.StartPosition, trajectory.LandingPosition,
                Mathf.Max(0f, trajectory.ApexPosition.y - trajectory.StartPosition.y), crossings);
        }

        private static IReadOnlyList<JumpSurfaceCrossing> CreateSurfaceCrossings(INavigationWorld world,
            JumpTrajectorySolution trajectory, Vector2 bodySize, CancellationToken cancellationToken)
        {
            List<JumpSurfaceCrossing> crossings = new();
            List<NavigationSurfaceCrossing> events = new();
            int samples = Mathf.Clamp(Mathf.CeilToInt(trajectory.FlightDuration / 0.02f), 16, 256);
            Vector2 previous = trajectory.StartPosition;
            for (int sample = 1; sample <= samples; sample++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Vector2 current = trajectory.GetPosition(trajectory.FlightDuration * sample / samples);
                events.Clear();
                world.CollectOneWayCrossings(previous, current, bodySize.x, events);
                for (int index = 0; index < events.Count; index++)
                {
                    NavigationSurfaceCrossing crossing = events[index];
                    JumpSurfaceCrossingKind kind = current.y >= previous.y
                        ? JumpSurfaceCrossingKind.Ascending : JumpSurfaceCrossingKind.Descending;
                    AddUnique(crossings, new JumpSurfaceCrossing(crossing.Surface, crossing.Position,
                        crossing.Normal, ((sample - 1) + crossing.Fraction) / samples, kind));
                }
                previous = current;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (world.TryResolveSupport(trajectory.LandingPosition, bodySize, Tolerance, out NavigationSupport landingSupport)
                && landingSupport.Kind == NavigationSurfaceKind.OneWay)
            {
                AddUnique(crossings, new JumpSurfaceCrossing(landingSupport.Surface, landingSupport.Position,
                    landingSupport.Normal, 1f, JumpSurfaceCrossingKind.Landing));
            }
            crossings.Sort((left, right) => left.Fraction.CompareTo(right.Fraction));
            return crossings.AsReadOnly();
        }

        private static void AddUnique(List<JumpSurfaceCrossing> crossings, JumpSurfaceCrossing candidate)
        {
            for (int index = 0; index < crossings.Count; index++)
                if (crossings[index].Surface == candidate.Surface && crossings[index].Kind == candidate.Kind
                    && Vector2.Distance(crossings[index].Position, candidate.Position) <= Tolerance) return;
            crossings.Add(candidate);
        }
    }
}
