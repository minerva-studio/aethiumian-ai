using Aethiumian.AI.Attributes;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Randomization;
using Aethiumian.AI.Variables;
using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Moves an entity toward a goal through planner-provided aerial actions.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Fly : Aethiumian.AI.Nodes.Movement
    {
        private const int MAXIMUM_WANDER_LOCATION_TRIALS = 20;

        [Header("Fly Parameter")]
        public VariableField<bool> setFinalPosition;
        public VariableField<float> speed;
        public VariableField<float> speedModifier = 1f;
        /// <summary>Fixed-tick velocity response coefficient in the inclusive range [0, 1]. The project must provide a finite value in this range.</summary>
        public VariableField<float> flexibility = 0.1f;
        /// <summary>Maximum target-center distance above the nearest support below, including one-way platforms.</summary>
        public VariableField<float> maxHeight = 16f;
        [DisplayIf(nameof(type), Behaviour.Trace)]
        public bool neverAboveMaxHeight;

        protected override NavigationGoalRequest BuildGoal(AABB target)
        {
            if (type == Behaviour.Trace && neverAboveMaxHeight)
            {
                Vector2 center = target.Center;
                Vector2 offset = LimitTargetHeight(center) - center;
                target = target.Translate(offset);
            }
            return CreateGoal(target, NavigationGoalGeometry.Proximity);
        }

        /// <summary>A Fly destination is a body-center position.</summary>
        protected override bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation)
        {
            if (path == PathMode.Naive)
            {
                Vector2 start = body.Center;
                Vector2 end = goal.TargetBounds.Center;
                NavigationRoute route = Vector2.Distance(start, end) <= NavigationWorldQueries.GeometryEpsilon
                    ? default
                    : NavigationRoute.Create(new[] { new FlyRouteSegment(start, end) }, true);
                operation = CompletedPlan(route);
                return true;
            }
            operation = NavigationRuntime.PlanFlyAsync(body, goal, new FlyNavigationParameters(RetreatExecution?.RemainingApproachDistance), extent, cancellation);
            return true;
        }

        protected override bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected)
        {
            connected = default;
            int furthest = -1;
            for (int i = 0; i < candidate.Count; i++)
            {
                if (candidate[i] is not FlyRouteSegment step) return false;
                if (!NavigationRuntime.IsBodyClearFlySegment(body, step.End - body.Center)) break;
                furthest = i;
            }
            if (furthest < 0) return false;
            var segments = new System.Collections.Generic.List<NavigationRouteSegment>
            { new FlyRouteSegment(body.Center, candidate[furthest].End) };
            for (int i = furthest + 1; i < candidate.Count; i++) segments.Add(candidate[i]);
            connected = NavigationRoute.Create(segments, candidate.ReachesGoal);
            return true;
        }

        protected override SegmentStartResult StartSegment(NavigationRouteSegment segment, AABB body)
        {
            if (segment is not FlyRouteSegment) throw new InvalidOperationException("Fly requires an aerial route action.");
            if (Executor is not FlyTraversalExecutor flight)
            {
                ContactFilter2D filter = TerrainFilter;
                int layerMask = filter.layerMask.value & ~RigidBody.excludeLayers.value & ~Collider.excludeLayers.value;
                filter.SetLayerMask(layerMask);
                flight = new FlyTraversalExecutor(RigidBody, Collider, speed * speedModifier, flexibility, filter, NavigationColliders, maxIdleDuration);
            }
            flight.SetWaypoint(body.Center, segment.End, Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon);
            return SegmentStartResult.Started(flight);
        }

        protected override void Finish(bool success)
        {
            if (success && type == Behaviour.Wander)
            {
                if (setFinalPosition && WanderDestination.HasValue)
                {
                    // Fly routes speak in the body-center frame, so the sampled destination is the
                    // final body's center and no anchor compensation is needed.
                    AABB body = NavigationBodyAabb;
                    AABB destinationBody = AABB.FromCenterAndSize(WanderDestination.Value, body.Size);
                    RigidBody.position += destinationBody.Center - body.Center;
                    RigidBody.linearVelocity = Vector2.zero;
                }
                return;
            }
            RigidBody.linearVelocity = Vector2.zero;
        }

        private Vector2 LimitTargetHeight(Vector2 target)
        {
            INavigationWorld world = NavigationWorld;
            if (world == null) return target;
            if (maxHeight == null || !maxHeight.HasValue)
                throw new ArgumentException("Fly maximum support height is required.", nameof(maxHeight));
            float limit = maxHeight;
            if (!NavigationNumeric.IsFinite(limit) || limit < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxHeight), limit, "Fly maximum support height must be finite and non-negative.");
            if (world.TryGetSupportBelow(target, out NavigationSupport support))
                target.y = Mathf.Min(target.y, support.Position.y + limit);
            return target;
        }

        protected override Vector2 GetWanderLocation(Vector2 center, AABB body)
        {
            for (int index = 0; index < MAXIMUM_WANDER_LOCATION_TRIALS; index++)
            {
                Vector2 point = behaviourTree.RandomSources.Resolve(this).NextUnitCircleDirection() * wanderDistance;
                Vector2 candidate = LimitTargetHeight(center + point);
                // Fly destinations are body centers. Lowering an authored sample may put the
                // body inside geometry, so validate the final point rather than the sample.
                AABB candidateBody = AABB.FromCenterAndSize(candidate, body.Size);
                if (!IsWanderCandidateAllowed(body.Center, candidate, candidateBody, false)) continue;

                return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. Is the entity outside the room?");
            return transform.position;
        }
    }
}
