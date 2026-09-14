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
        private const string JUMP_CALLBACK_METHOD_NAME = "OnJump";
        private const string WALK_CALLBACK_METHOD_NAME = "OnWalk";

        [Header("Walk Properties")]
        public VariableField<bool> setFinalPosition;
        public VariableField<float> accelerateRate = 0.5f;
        public VariableField<float> speed = 10f;
        /// <summary>Additional authored ratio applied to the node's final movement speed.</summary>
        public VariableField<float> speedModifier = 1f;
        /// <summary>Maximum apex displacement above the launch support; the solver selects a lower arc when possible.</summary>
        public VariableField<float> jumpHeight = 2f;
        public VariableField<float> jumpLength = 3f;
        public bool spriteFlip;

        [NonSerialized] private int unexpectedLandingRecoveryCount;
        private float NewFixedSpeed => speed * speedModifier;

        protected override NavigationGoalRequest BuildGoal(Bounds target, Bounds body, out Vector2 anchor)
        {
            anchor = new Vector2(body.center.x, body.min.y);
            return CreateGoal(target, NavigationGoalGeometry.GroundRange);
        }

        protected override bool TryRequestRoute(Vector2 start, NavigationGoalRegion goal,
            NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation)
        {
            operation = NavigationRuntime.PlanWalkAsync(start, goal.Request, CreateNavigationParameters(),
                PlanningExtent, cancellation, purpose);
            return true;
        }

        protected override bool TryConnectRoute(NavigationRoute candidate, Bounds body, out NavigationRoute connected)
        {
            connected = null;
            if (candidate.Count == 0) return false;
            if (candidate.Segments[0] is GroundRouteSegment)
                return TryReconnectNavigationRoute(candidate, NavigationGroundAnchor, out connected);
            if (!IsWithinContinuationTolerance(candidate.Start, NavigationGroundAnchor)) return false;
            connected = candidate;
            return true;
        }

        protected override bool IsGoalSatisfied(NavigationGoalRegion goal, Bounds body, bool swept) => goal.IsComplete(body.center, body.size) || swept;

        protected override bool TryRecover(ExecutionFailureReason reason, NavigationGoalRegion goal, Bounds body)
        {
            if (reason == ExecutionFailureReason.Obstructed) return true;
            if (reason != ExecutionFailureReason.UnexpectedSupport || unexpectedLandingRecoveryCount >= 2) return false;
            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out Vector2 support)
                || !NavigationRuntime.TryResolvePlanningGroundSupport(support, body.size, out _, out _)) return false;
            unexpectedLandingRecoveryCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MovementReplanDiagnostics.RecordUnexpectedLandingReplan();
#endif
            return true;
        }

        // protected override void OnTraversalCompletedWithoutSuccessor()
        //     => StopHorizontalVelocity();

        protected override void Finish(bool success, NavigationGoalRegion goal)
        {
            StopHorizontalVelocity();
            if (success && setFinalPosition && type == Behaviour.Wander)
            {
                RigidBody.position += goal.Center - NavigationGroundAnchor;
                RigidBody.linearVelocity = Vector2.zero;
            }
        }

        protected override ActionPreparation PrepareExecutor(NavigationRouteSegment segment, Bounds body, MovementExecutor reusable, out MovementExecutor prepared)
        {
            prepared = null;
            var groundExecutor = reusable as GroundTraversalExecutor;
            GroundTraversalExecutor GetExecutor()
            {
                if (groundExecutor != null) return groundExecutor;
                unexpectedLandingRecoveryCount = 0;
                return groundExecutor = new GroundTraversalExecutor(RigidBody, Collider, NavigationRuntime.CreateTerrainFilter(),
                    NewFixedSpeed, accelerateRate, DoWalkCallback, DoJumpCallback, NavigationColliders, MaximumIdleDuration);
            }
            switch (segment)
            {
                case GroundRouteSegment ground:
                    GetExecutor().SetGroundMove(NavigationGroundAnchor, ground.End);
                    UpdateSpriteFacing(segment.End);
                    prepared = groundExecutor;
                    return ActionPreparation.Ready;
                case JumpRouteSegment jump:
                    MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
                    INavigationWorld navigationWorld = NavigationWorld;
                    if (!navigation.TryResolvePlanningGroundSupport(
                        NavigationGroundAnchor, NavigationBodySize, out _, out NavigationSupport currentSupport))
                        return ActionPreparation.Waiting;
                    if (!navigation.TryResolvePlanningGroundSupport(
                        jump.LaunchSupport, NavigationBodySize, out _, out NavigationSupport launchSupport))
                        return ActionPreparation.Unavailable;
                    if (currentSupport.Surface != launchSupport.Surface)
                        return ActionPreparation.Unavailable;
                    if (!JumpTrajectory.IsApexHeightAllowed(jumpHeight, jump.MinimumApexHeight))
                        return ActionPreparation.Unavailable;

                    JumpTrajectoryInput input = new(
                        NavigationGroundAnchor,
                        jump.PlannedLanding,
                        Physics2D.gravity,
                        RigidBody.gravityScale,
                        RigidBody.linearDamping,
                        jumpHeight,
                        Time.fixedDeltaTime);
                    if (!JumpTrajectory.TrySolve(input, 512, jump.MinimumApexHeight, out JumpTrajectorySolution trajectory))
                        return ActionPreparation.Unavailable;
                    if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, jump, navigation, out OneWayPlatformCollisionLease lease))
                        return ActionPreparation.Unavailable;
                    try { GetExecutor().BeginJump(trajectory, lease); }
                    catch { lease?.Dispose(); throw; }
                    UpdateSpriteFacing(segment.End);
                    prepared = groundExecutor;
                    return ActionPreparation.Ready;
                case FallRouteSegment fall:
                    GetExecutor().BeginFall(fall.Start, fall.LedgeExit, fall.End);
                    UpdateSpriteFacing(segment.End);
                    prepared = groundExecutor;
                    return ActionPreparation.Ready;
                case DropThroughRouteSegment dropThrough:
                    GetExecutor().BeginDropThrough(dropThrough.Start, dropThrough.End);
                    UpdateSpriteFacing(segment.End);
                    prepared = groundExecutor;
                    return ActionPreparation.Ready;
                default:
                    throw new InvalidOperationException("Walk navigation produced an unsupported route segment.");
            }
        }

        private bool TryReconnectNavigationRoute(NavigationRoute route, Vector2 currentAnchor, out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = null;
            if (route == null || route.Count == 0 || route.Segments[0] is not GroundRouteSegment)
                return false;

            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            INavigationWorld snapshot = NavigationWorld;
            return WalkNavigationPlanner.TryReconnectGroundRoute(
                snapshot, route, currentAnchor, NavigationBodySize,
                NavigationWorldQueries.SupportSnapDistance, GroundTraversalEndpointPolicy.VerticalSupportTolerance,
                GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(
                    NewFixedSpeed, Time.fixedDeltaTime),
                out reconnectedRoute);
        }

        private WalkNavigationParameters CreateNavigationParameters()
            => new(
                NavigationBodySize,
                NewFixedSpeed,
                Physics2D.gravity,
                RigidBody.gravityScale,
                RigidBody.linearDamping,
                jumpHeight,
                jumpLength,
                Time.fixedDeltaTime);

        protected override Vector2Int GetWanderLocation(Vector2 center)
        {
            const int MAX_WANDER_LOCATION_TRIAL = 20;

            Vector2 wanderPosition;
            if (wanderDistance <= 0)
            {
                // Preserve zero-range wander behavior without calling RNG.NextFloat(0, 0).
                return Vector2Int.RoundToInt(center);
            }

            for (int i = 0; i < MAX_WANDER_LOCATION_TRIAL; i++)
            {
                var random = behaviourTree.RandomSources.Resolve(this);
                var x = random.NextFloat(-1f, 1f) * random.NextFloat(wanderDistance * 0.5f, wanderDistance * 1.5f);
                //wanderPosition = Vector2Int.RoundToInt(new Vector2(center.x + x, center.y));
                wanderPosition = center;
                wanderPosition.x += x;
                var fixedPosition = Vector2Int.RoundToInt(wanderPosition);
                if (IsValidNavigationWanderLocation(fixedPosition, true))
                    return fixedPosition;
            }
            Debug.LogWarning("Cannot find valid wander location around. is the entity outside the room?");
            return Vector2Int.FloorToInt(center);
        }

        private void StopHorizontalVelocity()
        {
            Vector2 velocity = RigidBody.linearVelocity;
            RigidBody.linearVelocity = new Vector2(0f, velocity.y);
        }

        private void UpdateSpriteFacing(Vector2 target)
        {
            if (!spriteFlip || !transform.TryGetComponent(out SpriteRenderer spriteRenderer)) return;
            spriteRenderer.flipX = target.x < NavigationGroundAnchor.x;
        }

        private void DoWalkCallback()
        {
            foreach (var item in gameObject.GetComponents<MonoBehaviour>())
            {
                var behaviour = item;
                try { CallbackTable.Call(ref behaviour, WALK_CALLBACK_METHOD_NAME); }
                catch { }
            }
        }

        private void DoJumpCallback()
        {
            foreach (var item in gameObject.GetComponents<MonoBehaviour>())
            {
                var behaviour = item;
                try { CallbackTable.Call(ref behaviour, JUMP_CALLBACK_METHOD_NAME); }
                catch { }
            }
        }
    }
}
