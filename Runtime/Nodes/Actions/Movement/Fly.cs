using Aethiumian.AI.Attributes;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Randomization;
using Aethiumian.AI.Variables;
using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Moves an entity through direct steering or a rolling aerial route.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Fly : Aethiumian.AI.Nodes.Movement
    {
        private const int MaximumWanderLocationTrials = 20;
        private static readonly Vector2[] RetreatEscapeDirections = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };

        [Header("Fly Parameter")]
        public VariableField<bool> setFinalPosition;
        public VariableField<float> speed;
        public VariableField<float> speedModifier = 1f;
        /// <summary>Fixed-tick velocity response coefficient in the inclusive range [0, 1].</summary>
        public VariableField<float> flexibility = 0.1f;
        /// <summary>Maximum target-center distance above the nearest support below, including one-way platforms.</summary>
        public VariableField<float> maxHeight = 16f;
        [DisplayIf(nameof(type), Behaviour.Trace)] public bool neverAboveMaxHeight;

        [NonSerialized] private FlyTraversalExecutor executor;
        [NonSerialized] private Vector2 steeringStart;


        private float FinalSpeed => speed * speedModifier;

        /// <summary>Height queries and Wander selection require the captured world's geometry.</summary>
        protected override bool RequiresNavigationWorldForInitialization => true;

        /// <summary>Validates component-backed executor inputs for one node execution.</summary>
        protected override void InitMovement()
        {
            _ = RigidBody;
            _ = Collider;
            ValidateFlexibility();
            if (type == Behaviour.Wander || type == Behaviour.Trace && neverAboveMaxHeight)
                _ = MaximumSupportHeight;
        }

        /// <summary>Advances direct flight or delegates route consumption to Movement.</summary>
        protected override void MovementFixedUpdate()
        {
            if (IsComplete) return;
            try
            {
                ValidateFlexibility();
                if (isSmart && type != Behaviour.Wander) Navigation.Tick();
                else TickDirectMovement();
            }
            catch (Exception exception)
            {
                CompleteWithException(exception);
            }
        }

        private void TickDirectMovement()
        {
            NavigationGoalRegion goalRegion = type == Behaviour.Wander
                ? null
                : RetreatExecution?.CurrentGoalRegion ?? GetNavigationGoalRegion();
            if (goalRegion != null && goalRegion.IsRetreat)
            {
                TickDirectRetreat(goalRegion);
                return;
            }

            // Arrival and direct steering use the same adjusted bounds. Re-reading the raw
            // tracing position here would disagree with the goal used by Smart planning.
            Vector2 target = type == Behaviour.Wander ? GoalProvider.GetDestination() : goalRegion.Center;
            if (type == Behaviour.Wander
                ? Vector2.Distance(NavigationCenterAnchor, target) <= reachDistance
                : (goalRegion?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false))
            {
                if (type == Behaviour.Wander) CompleteArrival(target);
                else CompleteNavigationPolicy();
                return;
            }

            executor ??= CreateExecutor();
            executor.SetSteeringTarget(target);
            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            if (result.Status == ExecutionStatus.Failed)
            {
                CompleteNavigationFailurePolicy();
                return;
            }
            bool reachedTarget = result.Status == ExecutionStatus.Completed;
            if (type == Behaviour.Wander)
            {
                if (reachedTarget) CompleteArrival(target);
                return;
            }

            if ((goalRegion?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false) || ObserveNavigationSweep(goalRegion))
                CompleteNavigationPolicy();
        }

        private void TickDirectRetreat(NavigationGoalRegion goalRegion)
        {
            if (goalRegion == null)
            {
                CompleteNavigationFailurePolicy();
                return;
            }
            if ((goalRegion?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false))
            {
                CompleteNavigationPolicy();
                return;
            }

            Vector2 away = GetGoalDirection(NavigationCenterAnchor, goalRegion);
            if (away.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                && !TryFindLocalRetreatDirection(goalRegion, out away))
            {
                CompleteNavigationFailurePolicy();
                return;
            }

            Vector2 target = NavigationCenterAnchor + away.normalized * Mathf.Max(1f, goalRegion.RetreatDistance + NavigationBodySize.magnitude);
            if (RetreatExecution != null
                && !RetreatExecution.AllowsSegment(goalRegion, NavigationCenterAnchor, target))
            {
                CompleteNavigationFailurePolicy();
                return;
            }

            executor ??= CreateExecutor();
            executor.SetSteeringTarget(target);
            if (executor.Tick(Time.fixedDeltaTime).Status == ExecutionStatus.Failed)
                CompleteNavigationFailurePolicy();
        }

        /// <summary>Retreat measures progress away from the threat in the goal coordinator.</summary>
        protected override bool MonitorRetreatStall => isBlind;

        /// <summary>Gets the body-center anchor used to splice aerial routes.</summary>
        protected override Vector2 NavigationRequestAnchor => NavigationCenterAnchor;

        /// <summary>Moves Trace target bounds relative to support without changing their size.</summary>
        protected override NavigationGoalRequest CreateNavigationGoalRequest()
        {
            NavigationGoalRequest request = base.CreateNavigationGoalRequest();
            Bounds bounds = request.TargetBounds;
            if (type == Behaviour.Trace && neverAboveMaxHeight)
            {
                Vector2 adjusted = LimitTargetHeight(bounds.center);
                bounds.center = new Vector3(adjusted.x, adjusted.y, bounds.center.z);
            }
            return request.WithTargetBounds(bounds);
        }

        /// <summary>Returns whether the current steering direction opposes the latest target direction.</summary>
        protected override bool IsCommittedTraversalReversed(
            NavigationRouteSegment segment,
            NavigationGoalRegion latestGoalRegion)
        {
            Vector2 movement = segment.End - NavigationCenterAnchor;
            Vector2 latest = GetGoalDirection(NavigationCenterAnchor, latestGoalRegion);
            return movement.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                && latest.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                && Vector2.Dot(movement, latest) < 0f;
        }

        /// <summary>Measures aerial route progress from the body center to a goal.</summary>
        protected override float NavigationDistanceToGoal(
            NavigationGoalRegion goalRegion,
            Vector2 anchor)
            => goalRegion.IsRetreat
                ? goalRegion.CompletionDistance(anchor, NavigationBodySize)
                : goalRegion.DistanceToCenteredBody(anchor, NavigationBodySize);

        /// <summary>Revalidates the first aerial segment against the current body clearance.</summary>
        protected override bool TryValidateNavigationRoute(NavigationRoute route)
        {
            if (route.Segments[0] is not FlyRouteSegment fly) return false;
            return RequireNavigationRuntime(nameof(Fly)).IsBodyClearFlySegment(
                NavigationCenterAnchor, fly.End, NavigationBodySize);
        }

        /// <summary>Applies Fly's waypoint and reversible sweep completion contract.</summary>
        protected override bool IsNavigationGoalReached(
            NavigationGoalRegion goalRegion, NavigationRouteSegment segment, bool sweptGoal)
            => goalRegion != null
                && (goalRegion.IsComplete(NavigationCenterAnchor, NavigationBodySize) || sweptGoal);

        /// <summary>Commits one fly waypoint without rebuilding the executor or clearing velocity.</summary>
        protected override NavigationSegmentCommitResult TryCommitNavigationSegment(NavigationRouteSegment segment)
        {
            if (segment is not FlyRouteSegment fly)
                throw new InvalidOperationException("Fly planning produced a non-fly route segment.");
            if (!RequireNavigationRuntime(nameof(Fly)).IsBodyClearFlySegment(NavigationCenterAnchor, fly.End, NavigationBodySize))
                return NavigationSegmentCommitResult.Rejected;
            NavigationGoalRegion goalRegion = Navigation.CurrentNavigationGoal;
            if (RetreatExecution != null
                && !RetreatExecution.AllowsSegment(goalRegion, NavigationCenterAnchor, fly.End))
                return NavigationSegmentCommitResult.Rejected;

            executor ??= CreateExecutor();
            steeringStart = NavigationCenterAnchor;
            hasPreviousNavigationCenter = false;
            return NavigationSegmentCommitResult.Committed;
        }

        /// <summary>Captures the dominant horizontal or vertical axis for a committed fly segment.</summary>
        private static Vector2 GetPrincipalSteeringDirection(Vector2 displacement)
        {
            if (displacement.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon)
                return Vector2.zero;

            if (Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y))
                return new Vector2(Mathf.Sign(displacement.x), 0f);

            return new Vector2(0f, Mathf.Sign(displacement.y));
        }

        /// <summary>Steers toward the current waypoint and consumes it once reached or passed.</summary>
        protected override ExecutionResult TickCommittedNavigationTraversal(NavigationRouteSegment segment)
        {
            Vector2 steeringTarget = segment.End;
            Vector2 steeringDirection = GetPrincipalSteeringDirection(steeringTarget - steeringStart);
            executor.SetSteeringTarget(steeringTarget, steeringDirection);
            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            // A physical failure takes precedence over waypoint consumption. The coordinator
            // may replan, but must never interpret abandonment as successful traversal.
            if (result.Status != ExecutionStatus.Running) return result;
            return HasPassedWaypoint(steeringTarget) ? ExecutionResult.Completed : result;
        }

        /// <summary>Selects the furthest body-clear endpoint in the current aerial route.</summary>
        protected override bool TrySelectNavigationSegment(
            NavigationRoute route,
            int routeIndex,
            out NavigationRouteSegment segment,
            out int nextRouteIndex)
        {
            int furthest = -1;
            for (int index = routeIndex; index < route.Count; index++)
            {
                if (route.Segments[index] is not FlyRouteSegment candidate)
                    throw new InvalidOperationException("Fly planning produced a non-fly route segment.");
                if (!RequireNavigationRuntime(nameof(Fly)).IsBodyClearFlySegment(NavigationCenterAnchor, candidate.End, NavigationBodySize)) break;
                if (RetreatExecution != null
                    && !RetreatExecution.AllowsRoute(route.GoalRegion, NavigationCenterAnchor, route, index)) continue;
                furthest = index;
            }

            if (furthest < 0)
            {
                segment = null;
                nextRouteIndex = routeIndex;
                return false;
            }

            segment = route.Segments[furthest];
            nextRouteIndex = furthest + 1;
            return true;
        }

        protected override bool IsNavigationSegmentConsumed(NavigationRouteSegment segment)
        {
            if (segment is not FlyRouteSegment) return false;
            Vector2 direction = segment.End - segment.Start;
            return (NavigationCenterAnchor - segment.End).sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                || direction.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                    && Vector2.Dot(NavigationCenterAnchor - segment.Start, direction) >= direction.sqrMagnitude;
        }

        private bool HasPassedWaypoint(Vector2 steeringTarget)
        {
            Vector2 segment = steeringTarget - steeringStart;
            return segment.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                || Vector2.Dot(NavigationCenterAnchor - steeringStart, segment) >= segment.sqrMagnitude;
        }

        /// <summary>Creates the Fly planner request from main-thread body geometry.</summary>
        protected override NavigationPlanningOperation CreateNavigationPlanningOperation(
            MapNavigationRuntime navigation,
            Vector2 start,
            NavigationGoalRequest goalRequest,
            CancellationToken cancellationToken,
            NavigationPlanningPurpose purpose)
        {
            return navigation.PlanFlyAsync(start, goalRequest,
                new FlyNavigationParameters(
                    NavigationBodySize,
                    RetreatExecution?.RemainingApproachDistance ?? 0f,
                    RetreatExecution?.HasApproachLimit ?? false), cancellationToken, purpose);
        }

        private FlyTraversalExecutor CreateExecutor()
        {
            ContactFilter2D filter = RequireNavigationRuntime(nameof(Fly)).CreateTerrainFilter();
            filter.SetLayerMask(filter.layerMask.value
                & ~RigidBody.excludeLayers.value
                & ~Collider.excludeLayers.value);
            return new FlyTraversalExecutor(RigidBody, Collider, FinalSpeed, Flexibility, filter, NavigationColliders);
        }

        private float Flexibility
        {
            get
            {
                float value = flexibility;
                if (!NavigationNumeric.IsFinite(value) || value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(flexibility), value,
                        "Fly flexibility must be finite and within [0, 1].");
                return value;
            }
        }

        private void ValidateFlexibility() => _ = Flexibility;

        private bool TryFindLocalRetreatDirection(NavigationGoalRegion goalRegion, out Vector2 direction)
        {
            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Fly));
            float step = Mathf.Max(navigation.CellSize, 0.25f);
            float bestCompletion = float.PositiveInfinity;
            direction = Vector2.zero;
            for (int index = 0; index < RetreatEscapeDirections.Length; index++)
            {
                Vector2 endpoint = NavigationCenterAnchor + RetreatEscapeDirections[index] * step;
                if (!navigation.IsBodyClearFlySegment(NavigationCenterAnchor, endpoint, NavigationBodySize)) continue;
                float completion = goalRegion.CompletionDistance(endpoint, NavigationBodySize);
                if (completion < bestCompletion - NavigationWorldQueries.GeometryEpsilon)
                {
                    bestCompletion = completion;
                    direction = RetreatEscapeDirections[index];
                }
            }

            return direction.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon;
        }

        private float MaximumSupportHeight
        {
            get
            {
                if (maxHeight == null || !maxHeight.HasValue)
                    throw new ArgumentException("Fly maximum support height is required.", nameof(maxHeight));
                float value = maxHeight;
                if (!NavigationNumeric.IsFinite(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(maxHeight), value,
                        "Fly maximum support height must be finite and non-negative.");
                return value;
            }
        }

        // A missing support means zero relative height for this policy: leave the target
        // unchanged. Do not fabricate a support that other navigation consumers could use.
        private Vector2 LimitTargetHeight(Vector2 target)
        {
            float limit = MaximumSupportHeight;
            if (!TryGetNavigationWorld(out INavigationWorld world))
                throw new InvalidOperationException("Fly target selection requires a ready navigation world.");
            if (world.TryGetSupportBelow(target, out NavigationSupport support))
                target.y = Mathf.Min(target.y, support.Position.y + limit);
            return target;
        }

        private void CompleteArrival(Vector2 target)
        {
            if (setFinalPosition && type != Behaviour.Trace)
            {
                RigidBody.position += target - NavigationCenterAnchor;
                RigidBody.linearVelocity = Vector2.zero;
            }
            else if (setFinalPosition)
            {
                RigidBody.linearVelocity = Vector2.zero;
            }
            CleanupNewBackend();
            Success();
        }

        /// <summary>Stops flight velocity and releases execution state before reporting the navigation result.</summary>
        protected override void FinishNavigation(bool success)
        {
            StopFlightVelocity();
            CleanupNewBackend();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementSuccess();
            else Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementFailure();
#endif
            if (success) Success();
            else Fail();
        }

        /// <summary>Clears the full velocity owned by goal-based Fly completion.</summary>
        private void StopFlightVelocity()
        {
            RigidBody.linearVelocity = Vector2.zero;
        }

        private void CompleteWithException(Exception exception)
        {
            CleanupNewBackend();
            Exception(exception);
        }

        /// <summary>Returns the capability-owned direction for the current goal semantics.</summary>
        private Vector2 GetGoalDirection(Vector2 anchor, NavigationGoalRegion goalRegion)
            => goalRegion.IsRetreat ? anchor - goalRegion.Center : goalRegion.Center - anchor;

        private void CleanupNewBackend()
        {
            CleanupNavigationLifecycle();
            executor?.Dispose();
            executor = null;
        }

        /// <summary>Chooses a valid authored wander destination.</summary>
        protected override Vector2Int GetWanderLocation(Vector2 center)
        {
            if (!TryGetNavigationWorld(out INavigationWorld world))
                throw new InvalidOperationException("Fly Wander selection requires a ready navigation world.");
            for (int index = 0; index < MaximumWanderLocationTrials; index++)
            {
                Vector2 point = behaviourTree.RandomSources.Resolve(this).NextUnitCircleDirection() * wanderDistance;
                Vector2Int candidate = Vector2Int.FloorToInt(center + point);
                candidate = Vector2Int.FloorToInt(LimitTargetHeight(candidate));
                // Fly destinations are body centers. Lowering and integer rounding may put
                // the body inside geometry, so validate the final point rather than the sample.
                if (world.AreInSameRegion(NavigationCenterAnchor, candidate)
                    && world.IsBodyClear(new Rect((Vector2)candidate - NavigationBodySize * 0.5f,
                        NavigationBodySize), 0f))
                    return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. Is the entity outside the room?");
            return Vector2Int.FloorToInt(transform.position);
        }

        /// <summary>Releases node-owned runtime state without clearing externally owned velocity.</summary>
        public override void OnDestroy() => CleanupNewBackend();
    }
}
