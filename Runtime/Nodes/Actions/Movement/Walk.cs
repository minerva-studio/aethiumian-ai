using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Walk : Aethiumian.AI.Nodes.Movement
    {
        [Header("Walk Properties")]
        public VariableField<bool> setFinalPosition;
        public VariableField<float> accelerateRate = 0.5f;
        public VariableField<float> speed = 10f;
        /// <summary>Additional authored ratio applied to the node's final movement speed.</summary>
        public VariableField<float> speedModifier = 1f;
        /// <summary>Maximum apex displacement above the launch support; the solver selects a lower arc when possible.</summary>
        public VariableField<float> jumpHeight = 2f;
        public VariableField<float> jumpLength = 3f;

        [NonSerialized] private int unexpectedLandingRecoveryCount;
        private float NewFixedSpeed => speed * speedModifier;

        protected override NavigationGoalRequest BuildGoal(AABB target) => CreateGoal(target, NavigationGoalGeometry.GroundRange);

        protected override bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation)
        {
            if (path == PathMode.Naive)
            {
                Vector2 start = body.LowerCenter;
                Vector2 end = new(goal.TargetBounds.CenterX, start.y);
                NavigationRoute route = Vector2.Distance(start, end) <= NavigationWorldQueries.GeometryEpsilon
                    ? default
                    : NavigationRoute.Create(new[] { new GroundRouteSegment(start, end) }, true);
                operation = CompletedPlan(route);
                return true;
            }
            operation = NavigationRuntime.PlanWalkAsync(body, goal, CreateNavigationParameters(), extent, cancellation);
            return true;
        }

        protected override bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected)
        {
            connected = default;
            if (candidate.Count == 0) return false;
            if (candidate[0] is GroundRouteSegment)
                return WalkNavigationPlanner.TryReconnectGroundRoute(NavigationWorld, candidate, body, GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(NewFixedSpeed), out connected);
            if (candidate[0] is JumpRouteSegment)
            {
                // A plan speaks in support space, while the body anchor rests one contact gap above
                // the surface it stands on; resolve the anchor before comparing the two positions.
                if (!NavigationRuntime.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport currentSupport))
                    return false;
                Vector2 offset = candidate.Start - currentSupport.Position;
                float horizontalTolerance = GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(NewFixedSpeed);
                if (Mathf.Abs(offset.x) > horizontalTolerance || Mathf.Abs(offset.y) > GroundTraversalEndpointPolicy.VerticalSupportTolerance)
                    return false;
            }
            else if (!IsWithinContinuationTolerance(candidate.Start, body.LowerCenter))
            {
                return false;
            }
            connected = candidate;
            return true;
        }

        protected override bool TryRecover(ExecutionFailureReason reason, AABB body)
        {
            if (base.TryRecover(reason, body)) return true;
            if (reason == ExecutionFailureReason.UnexpectedSupport && unexpectedLandingRecoveryCount < 2)
            {
                if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out Vector2 support)) return false;
                AABB supportBody = AABB.FromLowerCenter(support, body.Size);
                if (!NavigationRuntime.TryResolvePlanningGroundSupport(supportBody, out _, out _)) return false;
                unexpectedLandingRecoveryCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                MovementReplanDiagnostics.RecordUnexpectedLandingReplan();
#endif
                return true;
            }
            return false;
        }

        protected override void Finish(bool success)
        {
            Vector2 velocity = RigidBody.linearVelocity;
            RigidBody.linearVelocity = new Vector2(0f, velocity.y);
            if (success && setFinalPosition && type == Behaviour.Wander && WanderDestination.HasValue)
            {
                // Walk routes speak in the ground-anchor frame, so the sampled destination is the
                // final body's lower center and no anchor compensation is needed.
                AABB body = NavigationBodyAabb;
                AABB destinationBody = AABB.FromLowerCenter(WanderDestination.Value, body.Size);
                RigidBody.position += destinationBody.LowerCenter - body.LowerCenter;
                RigidBody.linearVelocity = Vector2.zero;
            }
        }

        protected override SegmentStartResult StartSegment(NavigationRouteSegment segment, AABB body)
        {
            var groundExecutor = Executor as GroundTraversalExecutor;

            GroundTraversalExecutor GetExecutor()
            {
                if (groundExecutor != null) return groundExecutor;
                unexpectedLandingRecoveryCount = 0;
                return groundExecutor = new GroundTraversalExecutor(RigidBody, Collider, TerrainFilter, NewFixedSpeed, accelerateRate, NavigationColliders, maxIdleDuration);
            }

            switch (segment)
            {
                case GroundRouteSegment ground:
                    GetExecutor().SetGroundMove(body.LowerCenter, ground.End);
                    return SegmentStartResult.Started(groundExecutor);
                case JumpRouteSegment jump:
                    MapNavigationRuntime navigation = NavigationRuntime;
                    INavigationWorld navigationWorld = NavigationWorld;
                    if (!navigation.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport currentSupport))
                        return SegmentStartResult.Pending;
                    if (!navigation.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, body.Size), out _, out NavigationSupport launchSupport))
                        return SegmentStartResult.Rejected;
                    if (currentSupport.Surface != launchSupport.Surface)
                        return SegmentStartResult.Rejected;
                    if (!JumpTrajectory.IsApexHeightAllowed(jumpHeight, jump.MinimumApexHeight))
                        return SegmentStartResult.Rejected;

                    if (!navigation.TryGetJumpSolver(out GroundJumpSolver solver))
                        return SegmentStartResult.Pending;

                    GroundJumpParameters parameters = CreateNavigationParameters().GetJumpParameters(body.Size);
                    if (!solver.TrySolve(body.LowerCenter, jump.End, parameters, out JumpTrajectorySolution trajectory))
                        return SegmentStartResult.Rejected;

                    JumpRouteSegment resolvedSegment = GroundJumpGeometry.CreateSegment(navigationWorld, trajectory, body.Size);
                    if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, resolvedSegment, navigation, out OneWayPlatformCollisionLease lease))
                        return SegmentStartResult.Rejected;
                    try
                    {
                        GetExecutor().BeginJump(trajectory, lease);
                    }
                    catch { lease?.Dispose(); throw; }
                    return SegmentStartResult.Started(groundExecutor);
                case FallRouteSegment fall:
                    GetExecutor().BeginFall(fall.Start, fall.LedgeExit, fall.End);
                    return SegmentStartResult.Started(groundExecutor);
                case DropThroughRouteSegment dropThrough:
                    GetExecutor().BeginDropThrough(dropThrough.Start, dropThrough.End);
                    return SegmentStartResult.Started(groundExecutor);
                default:
                    throw new InvalidOperationException("Walk navigation produced an unsupported route segment.");
            }
        }

        private WalkNavigationParameters CreateNavigationParameters() => new(NewFixedSpeed, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength, Time.fixedDeltaTime);

        protected override Vector2 GetWanderLocation(Vector2 center, AABB body)
        {
            const int MAXIMUM_WANDER_LOCATION_TRIALS = 20;

            if (wanderDistance <= 0)
            {
                // Preserve zero-range wander behavior without calling RNG.NextFloat(0, 0).
                return center;
            }

            for (int i = 0; i < MAXIMUM_WANDER_LOCATION_TRIALS; i++)
            {
                var random = behaviourTree.RandomSources.Resolve(this);
                var x = random.NextFloat(-1f, 1f) * random.NextFloat(wanderDistance * 0.5f, wanderDistance * 1.5f);
                var candidate = new Vector2(center.x + x, center.y);
                if (IsWanderCandidateAllowed(body.Center, candidate, AABB.FromLowerCenter(candidate, body.Size), true))
                    return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. is the entity outside the room?");
            return center;
        }
    }
}
