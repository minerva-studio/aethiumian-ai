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

        /// <summary>Gets the new backend speed resolved exclusively from node-authored values.</summary>
        private float NewFixedSpeed => speed * speedModifier;

        [NonSerialized] private int unexpectedLandingRecoveryCount;

        /// <summary>Gets the executor owned by the current Walk run, if initialized.</summary>
        [field: NonSerialized]
        public GroundTraversalExecutor TraversalExecutor { get; private set; }


        /// <summary>Creates the execution state owned by one new-backend node run.</summary>
        protected override void InitMovement()
        {
            unexpectedLandingRecoveryCount = 0;
            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            TraversalExecutor = new GroundTraversalExecutor(
                RigidBody,
                Collider,
                navigation.CreateTerrainFilter(),
                NewFixedSpeed,
                accelerateRate,
                DoWalkCallback,
                DoJumpCallback,
                NavigationColliders,
                MaximumIdleDuration);

        }

        /// <summary>Starts Direct execution or prepares the shared rolling-route lifecycle.</summary>
        protected override void StartMovement()
        {
            unexpectedLandingRecoveryCount = 0;
            if (type == Behaviour.Retreat)
                throw new NotSupportedException($"{GetType().Name} does not provide Retreat traversal.");

            if (IsComplete || !movementSource.CanMove) return;
            Vector2 initialDestination = GoalProvider.GetDestination();
            bool alreadyArrived = TryCreateWalkGoalRegion(out NavigationGoalRegion initialGoal)
                && initialGoal.IsComplete(NavigationCenterAnchor, NavigationBodySize);
            if (alreadyArrived)
            {
                CompleteNewMovement(true, initialDestination);
                return;
            }

        }

        /// <summary>Advances the active Direct or Navigate execution from the behaviour tree fixed step.</summary>
        protected override void MovementFixedUpdate()
        {
            if (IsComplete || TraversalExecutor == null) return;

            try
            {
                Vector2 currentDestination = GoalProvider.GetDestination();
                UpdateSpriteFacing(currentDestination);
                if (type != Behaviour.Wander)
                {
                    Navigation.Tick();
                }
                else
                {
                    TickDirect(currentDestination);
                }
            }
            catch (Exception exception)
            {
                CleanupNewMovement();
                Exception(exception);
            }
        }

        /// <summary>Executes the immutable Wander destination as one direct ground-movement step.</summary>
        private void TickDirect(Vector2 currentDestination)
        {
            if (!TryCreateWalkGoalRegion(out NavigationGoalRegion goalRegion)) return;
            if ((goalRegion?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false))
            {
                CompleteNewMovement(true, currentDestination);
                return;
            }

            if (TraversalExecutor.CurrentAction == GroundTraversalExecutor.ActionKind.None)
            {
                TraversalExecutor.BeginGroundMove(NavigationGroundAnchor, currentDestination);
            }

            var directResult = TraversalExecutor.Tick(Time.fixedDeltaTime);
            RecordStallFailure(directResult);
            if (directResult.Status == ExecutionStatus.Running) return;

            if (directResult.Status == ExecutionStatus.Completed && (goalRegion?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false))
            {
                CompleteNewMovement(true, currentDestination);
            }
            else
            {
                CompleteNewMovement(false, currentDestination);
            }
        }

        /// <summary>Gets the current ground anchor used by route planning and splicing.</summary>
        protected override Vector2 NavigationRequestAnchor => NavigationGroundAnchor;

        /// <summary>Default Walk goals retain the legacy GroundRange geometry.</summary>
        protected override NavigationGoalGeometry DefaultGoalGeometry => NavigationGoalGeometry.GroundRange;

        /// <summary>Binds every Walk target to the shared Ground Walk arrival contract.</summary>
        private bool TryCreateWalkGoalRegion(out NavigationGoalRegion goalRegion)
        {
            NavigationGoalRequest request = CreateNavigationGoalRequest();
            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            if (!navigation.TryGetWorld(out INavigationWorld snapshot))
            {
                goalRegion = null;
                return false;
            }

            goalRegion = NavigationGoalRegion.Bind(request, snapshot);
            return true;
        }

        /// <summary>Simple Walk intentionally consumes only one locally selected traversal.</summary>
        protected override bool CompleteAfterOneNavigationSegment => !isSmart;

        /// <summary>Validates a route against the current grounded anchor before publication.</summary>
        protected override bool TryValidateNavigationRoute(NavigationRoute route)
        {
            NavigationRouteSegment first = route.Segments[0];
            if (first is not GroundRouteSegment ground) return true;

            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            if (!navigation.TryResolvePlanningGroundSupport(
                NavigationGroundAnchor, NavigationBodySize, out _, out _)) return false;

            Vector2 direction = ground.End - ground.Start;
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= NavigationWorldQueries.GeometryEpsilon) return true;
            Vector2 offset = NavigationGroundAnchor - ground.Start;
            float parameter = Vector2.Dot(offset, direction) / lengthSquared;
            float distanceFromLine = Mathf.Abs(direction.x * offset.y - direction.y * offset.x)
                / Mathf.Sqrt(lengthSquared);
            float parameterTolerance = NavigationWorldQueries.SupportSnapDistance / Mathf.Sqrt(lengthSquared);
            return parameter >= -parameterTolerance
                && parameter <= 1f + parameterTolerance
                && distanceFromLine <= NavigationWorldQueries.SupportSnapDistance;
        }

        /// <summary>Builds a safe continuous-ground route to the current goal without background search.</summary>
        public override bool TryCreateDirectNavigationRoute(NavigationGoalRegion goal, out NavigationRoute route)
        {
            route = null;
            if (goal == null || goal.IsRetreat) return false;
            Vector2 start = NavigationGroundAnchor;
            Vector2 end = new(goal.Center.x, start.y);
            INavigationWorld world = goal.Snapshot;
            if (!world.TryResolveGroundSupport(start, NavigationBodySize,
                NavigationWorldQueries.SupportSnapDistance, out start, out _)) return false;
            end.y = start.y;
            if (!goal.IsComplete(end + Vector2.up * (NavigationBodySize.y * 0.5f), NavigationBodySize)
                || !WalkNavigationPlanner.TryValidateGroundConnection(world, start, end, NavigationBodySize,
                    NavigationWorldQueries.SupportSnapDistance, GroundTraversalEndpointPolicy.VerticalSupportTolerance))
                return false;
            route = NavigationRoute.Create(start, goal, end, new[] { new GroundRouteSegment(start, end) }, true);
            return true;
        }

        protected override bool IsNavigationSegmentConsumed(NavigationRouteSegment segment)
        {
            if (segment is not GroundRouteSegment ground) return false;
            Vector2 anchor = NavigationGroundAnchor;
            if (Mathf.Abs(anchor.y - ground.End.y) > NavigationWorldQueries.SupportSnapDistance) return false;
            float tolerance = GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(NewFixedSpeed, Time.fixedDeltaTime);
            return Mathf.Abs(anchor.x - ground.End.x) <= tolerance
                || Mathf.Abs(ground.End.x - ground.Start.x) > tolerance
                    && (anchor.x - ground.End.x) * (ground.End.x - ground.Start.x) >= 0f;
        }

        /// <summary>Reconnects an uncommitted Ground segment through planner geometry.</summary>
        protected override bool TryReconnectNavigationRoute(
            NavigationRoute route, Vector2 currentAnchor, out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = null;
            if (route == null || route.Count == 0 || route.Segments[0] is not GroundRouteSegment)
                return false;

            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            if (!navigation.TryGetWorld(out INavigationWorld snapshot)) return false;
            return WalkNavigationPlanner.TryReconnectGroundRoute(
                snapshot, route, currentAnchor, NavigationBodySize,
                NavigationWorldQueries.SupportSnapDistance, GroundTraversalEndpointPolicy.VerticalSupportTolerance,
                GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(
                    NewFixedSpeed, Time.fixedDeltaTime),
                out reconnectedRoute);
        }

        /// <summary>Applies Walk's GroundMove completion contract to a committed navigation action.</summary>
        protected override bool IsNavigationGoalReached(
            NavigationGoalRegion goalRegion, NavigationRouteSegment segment, bool sweptGoal)
            => goalRegion != null
                && (goalRegion.IsComplete(NavigationCenterAnchor, NavigationBodySize)
                    || segment is GroundRouteSegment && sweptGoal);

        /// <summary>Cancels the current reversible ground action without clearing Rigidbody velocity.</summary>
        protected override void CancelCommittedTraversal()
        {
            TraversalExecutor.Cancel();
            base.CancelCommittedTraversal();
        }

        /// <summary>Reinitializes the executor's phase baseline after Movement pauses this node.</summary>
        protected override void ResetTraversalProgressBaseline()
        {
            base.ResetTraversalProgressBaseline();
            TraversalExecutor?.ResetProgressBaseline();
        }

        /// <summary>Commits one route segment as a low-level physical action.</summary>
        protected override NavigationSegmentCommitResult TryCommitNavigationSegment(NavigationRouteSegment segment)
        {
            switch (segment)
            {
                case GroundRouteSegment ground:
                    TraversalExecutor.BeginGroundMove(NavigationGroundAnchor, ground.End);
                    hasPreviousNavigationCenter = false;
                    return NavigationSegmentCommitResult.Committed;
                case JumpRouteSegment jump:
                    MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
                    if (!navigation.TryGetWorld(out INavigationWorld navigationWorld))
                        return NavigationSegmentCommitResult.Deferred;
                    if (!navigation.TryResolvePlanningGroundSupport(
                        NavigationGroundAnchor, NavigationBodySize, out _, out NavigationSupport currentSupport))
                        return NavigationSegmentCommitResult.Deferred;
                    if (!navigation.TryResolvePlanningGroundSupport(
                        jump.LaunchSupport, NavigationBodySize, out _, out NavigationSupport launchSupport))
                        return NavigationSegmentCommitResult.Rejected;
                    if (currentSupport.Surface != launchSupport.Surface)
                        return NavigationSegmentCommitResult.Rejected;
                    if (!JumpTrajectory.IsApexHeightAllowed(jumpHeight, jump.MinimumApexHeight))
                        return NavigationSegmentCommitResult.Rejected;

                    JumpTrajectoryInput input = new(
                        NavigationGroundAnchor,
                        jump.PlannedLanding,
                        Physics2D.gravity,
                        RigidBody.gravityScale,
                        RigidBody.linearDamping,
                        jumpHeight,
                        Time.fixedDeltaTime);
                    if (!JumpTrajectory.TrySolve(input, 512, jump.MinimumApexHeight, out JumpTrajectorySolution trajectory))
                        return NavigationSegmentCommitResult.Rejected;
                    if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, jump, navigation, out OneWayPlatformCollisionLease lease))
                        return NavigationSegmentCommitResult.Rejected;
                    TraversalExecutor.BeginJump(trajectory, lease);
                    hasPreviousNavigationCenter = false;
                    return NavigationSegmentCommitResult.Committed;
                case FallRouteSegment fall:
                    TraversalExecutor.BeginFall(fall.Start, fall.LedgeExit, fall.End);
                    hasPreviousNavigationCenter = false;
                    return NavigationSegmentCommitResult.Committed;
                case DropThroughRouteSegment dropThrough:
                    TraversalExecutor.BeginDropThrough(dropThrough.Start, dropThrough.End);
                    hasPreviousNavigationCenter = false;
                    return NavigationSegmentCommitResult.Committed;
                default:
                    throw new InvalidOperationException("Walk navigation produced an unsupported route segment.");
            }
        }

        /// <summary>Advances the currently committed ground action.</summary>
        protected override ExecutionResult TickCommittedNavigationTraversal(NavigationRouteSegment segment)
        {
            ExecutionResult result = TraversalExecutor.Tick(Time.fixedDeltaTime);
            RecordStallFailure(result);
            return result;
        }

        /// <summary>Restarts Smart Walk from a physically observed support after an unexpected landing.</summary>
        protected override void HandleUnexpectedNavigationLanding(NavigationGoalRegion goalRegion)
        {
            if (!isSmart)
            {
                base.HandleUnexpectedNavigationLanding(goalRegion);
                return;
            }
            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Walk));
            // The executor has already released its action. Re-read current physical support
            // synchronously; no stale contact point is forwarded through route coordination.
            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, navigation.CreateTerrainFilter(), out Vector2 support)
                || !navigation.TryResolvePlanningGroundSupport(support, NavigationBodySize, out _, out _)
                || unexpectedLandingRecoveryCount >= 2)
            {
                base.HandleUnexpectedNavigationLanding(goalRegion);
                return;
            }
            unexpectedLandingRecoveryCount++;

            Navigation.RestartFromPhysicalState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordUnexpectedLandingReplan();
#endif
            Navigation.TryBeginPlanning(NavigationRequestAnchor, goalRegion);
        }

        /// <summary>Creates the Walk planner request from main-thread physics values.</summary>
        protected override NavigationPlanningOperation CreateNavigationPlanningOperation(
            MapNavigationRuntime navigation,
            Vector2 start,
            NavigationGoalRequest goalRequest,
            CancellationToken cancellationToken,
            NavigationPlanningPurpose purpose)
        {
            WalkNavigationParameters parameters = CreateNavigationParameters();
            return isSmart
                ? navigation.PlanWalkAsync(start, goalRequest, parameters, cancellationToken, purpose)
                : navigation.PlanWalkStepAsync(start, goalRequest, parameters, cancellationToken);
        }

        /// <summary>Captures the current project physics values used by one Walk request.</summary>
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

        /// <summary>Applies the authored final-position behavior, releases owned state, and completes the node.</summary>
        private void CompleteNewMovement(bool succeeded, Vector2 finalDestination)
        {
            StopHorizontalVelocity();
            if (succeeded && setFinalPosition)
            {
                if (!isSmart)
                {
                    Vector2 displacement = finalDestination - NavigationGroundAnchor;
                    RigidBody.position += displacement;
                }
                RigidBody.linearVelocity = Vector2.zero;
            }

            CleanupNewMovement();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (succeeded) Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementSuccess();
            else Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementFailure();
#endif
            End(succeeded);
        }

        /// <summary>Completes ordinary navigation without teleporting or clearing externally useful velocity.</summary>
        protected override void FinishNavigation(bool success)
        {
            StopHorizontalVelocity();
            CleanupNewMovement();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementSuccess();
            else Aethiumian.AI.Navigation.Diagnostics.MovementReplanDiagnostics.RecordMovementFailure();
#endif
            End(success);
        }

        /// <summary>Clears only the horizontal velocity owned by Walk while preserving vertical physics.</summary>
        private void StopHorizontalVelocity()
        {
            Vector2 velocity = RigidBody.linearVelocity;
            RigidBody.linearVelocity = new Vector2(0f, velocity.y);
        }

        /// <summary>Cancels and releases all runtime state owned by the new backend.</summary>
        private void CleanupNewMovement()
        {
            CleanupNavigationLifecycle();
            TraversalExecutor?.Dispose();
            TraversalExecutor = null;
            unexpectedLandingRecoveryCount = 0;
        }

        /// <summary>Updates the optional facing sprite without affecting physics state.</summary>
        private void UpdateSpriteFacing(Vector2 target)
        {
            if (!spriteFlip || !transform.TryGetComponent(out SpriteRenderer spriteRenderer)) return;
            spriteRenderer.flipX = target.x < NavigationGroundAnchor.x;
        }


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

        private void DoWalkCallback()
        {
            foreach (var item in gameObject.GetComponents<MonoBehaviour>())
            {
                var behaviour = item;
                try { CallbackTable.Call(ref behaviour, WALK_CALLBACK_METHOD_NAME); }
                catch { }
            }
        }

        /// <summary>Invokes the established jump callback on behaviours hosted beside the AI.</summary>
        private void DoJumpCallback()
        {
            foreach (var item in gameObject.GetComponents<MonoBehaviour>())
            {
                var behaviour = item;
                try { CallbackTable.Call(ref behaviour, JUMP_CALLBACK_METHOD_NAME); }
                catch { }
            }
        }

        public override void OnDestroy()
        {
            CleanupNewMovement();
        }
    }
}
