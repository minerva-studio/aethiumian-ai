using Aethiumian.AI;
using Aethiumian.AI.Navigation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif
using Aethiumian.AI.Variables;
using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Moves an entity through direct or planned ballistic jumps.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Jump : Aethiumian.AI.Nodes.Movement
    {
        private const string JumpCallbackMethodName = "OnJump";

        [Header("Jump Property")]
        /// <summary>Maximum apex displacement above the launch support; the solver selects a lower arc when possible.</summary>
        public VariableField<float> jumpHeight = 3f;
        public VariableField<float> jumpLength = 3f;
        public VariableField<float> jumpInterval = 1.5f;
        /// <summary>Scales only the interval between launches; it does not change jump physics.</summary>
        public VariableField<float> speedModifier = 1f;

        private float jumpCountDown;
        [NonSerialized] private BallisticJumpExecutor executor;
        // Captured for each node run; this is configuration, not a cached support observation.
        [NonSerialized] private ContactFilter2D terrainFilter;

        /// <summary>Validates authored jump capability values.</summary>
        public override bool EditorCheck(BehaviourTreeData tree)
        {
            if (jumpHeight.IsConstant && jumpHeight < 0f)
            {
                Debug.LogError($"Jump height of {name} is less than 0, this is not allowed", gameObject);
                return false;
            }
            if (jumpLength.IsConstant && jumpLength < 0f)
            {
                Debug.LogError($"Jump length of {name} is less than 0, this is not allowed", gameObject);
                return false;
            }
            return true;
        }

        /// <summary>Initializes the jump cadence for one node execution.</summary>
        protected override void InitMovement()
        {
            jumpCountDown = 0f;
            ValidateJumpCadence();
            terrainFilter = RequireNavigationRuntime(nameof(Jump)).CreateTerrainFilter();
        }

        /// <summary>Advances direct jumping or delegates route consumption to Movement.</summary>
        protected override void MovementFixedUpdate()
        {
            if (IsComplete) return;
            if (type == Behaviour.Retreat)
                throw new NotSupportedException($"{GetType().Name} does not provide Retreat traversal.");

            try
            {
                ValidateJumpCadence();
                jumpCountDown = Mathf.Max(0f, jumpCountDown - Time.fixedDeltaTime);
                if (isSmart && type != Behaviour.Wander) Navigation.Tick();
                else TickDirectJump();
            }
            catch (Exception exception)
            {
                CompleteWithException(exception);
            }
        }

        /// <summary>Executes repeated direct jumps toward the latest destination.</summary>
        private void TickDirectJump()
        {
            bool hadActiveJump = executor != null;
            ExecutionResult directResult = TickCommittedNavigationTraversal(null);
            if (IsComplete || directResult.Status == ExecutionStatus.Running) return;
            if (directResult.Status == ExecutionStatus.Failed)
            {
                CompleteFailure();
                return;
            }

            Vector2 target = GoalProvider.GetDestination();
            if (GetNavigationGoalRegion()?.IsComplete(NavigationCenterAnchor, NavigationBodySize) ?? false)
            {
                if (hadActiveJump || IsSupportedAndNotRising())
                    CompleteSuccess();
                return;
            }
            if (!IsOnGround() || jumpCountDown > 0f) return;

            Vector2 start = NavigationGroundAnchor;
            Vector2 landing = target;
            landing.x = start.x + Mathf.Clamp(target.x - start.x, -jumpLength, jumpLength);
            if (!JumpTrajectory.TrySolve(CreateTrajectoryInput(start, landing), out JumpTrajectorySolution trajectory))
            {
                CompleteFailure();
                return;
            }

            BeginJump(trajectory, null);
        }

        /// <summary>Allows an initially reached Jump to complete only from a stable physical support.</summary>
        private bool IsSupportedAndNotRising()
            => RigidBody.linearVelocity.y <= 0f
                && NavigationWorldQueries.TryGetGroundSupportPoint(Collider, terrainFilter, out _);

        /// <summary>Gets the actual grounded support anchor used for jump route splicing.</summary>
        protected override Vector2 NavigationRequestAnchor => NavigationGroundAnchor;

        /// <summary>Checks that a deferred jump route still launches from the current support.</summary>
        protected override bool TryValidateNavigationRoute(NavigationRoute route)
        {
            if (route.Segments[0] is not JumpRouteSegment jump) return false;
            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Jump));
            if (!navigation.TryResolvePlanningGroundSupport(
                NavigationGroundAnchor, NavigationBodySize, out _, out NavigationSupport currentSupport))
                return false;
            if (!navigation.TryResolvePlanningGroundSupport(
                jump.LaunchSupport, NavigationBodySize, out _, out NavigationSupport launchSupport))
                return false;
            return currentSupport.Surface == launchSupport.Surface;
        }

        /// <summary>Allows Jump to finish only after its ballistic executor reports a valid landing.</summary>
        protected override bool IsNavigationGoalReached(
            NavigationGoalRegion goalRegion, NavigationRouteSegment segment, bool sweptGoal)
            => goalRegion != null && goalRegion.IsComplete(NavigationCenterAnchor, NavigationBodySize);

        /// <summary>Commits one planned jump after resolving it from the actual Rigidbody state.</summary>
        protected override NavigationSegmentCommitResult TryCommitNavigationSegment(NavigationRouteSegment segment)
        {
            if (segment is not JumpRouteSegment jump)
                throw new InvalidOperationException("Jump planning produced a non-jump route segment.");
            if (jumpCountDown > 0f) return NavigationSegmentCommitResult.Deferred;

            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Jump));
            if (!navigation.TryResolvePlanningGroundSupport(
                NavigationGroundAnchor, NavigationBodySize, out _, out NavigationSupport currentSupport))
                return NavigationSegmentCommitResult.Deferred;
            if (!navigation.TryResolvePlanningGroundSupport(
                jump.LaunchSupport, NavigationBodySize, out _, out NavigationSupport launchSupport))
                return NavigationSegmentCommitResult.Rejected;
            if (currentSupport.Surface != launchSupport.Surface)
                return NavigationSegmentCommitResult.Rejected;
            if (!navigation.TryGetWorld(out INavigationWorld navigationWorld))
                return NavigationSegmentCommitResult.Deferred;
            if (!navigation.TryGetJumpSolver(out GroundJumpSolver jumpSolver))
                return NavigationSegmentCommitResult.Deferred;
            GroundJumpParameters parameters = new(NavigationBodySize, Physics2D.gravity,
                RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength,
                Time.fixedDeltaTime, NavigationWorldQueries.SupportSnapDistance,
                GroundTraversalEndpointPolicy.VerticalSupportTolerance);
            if (!jumpSolver.TrySolve(NavigationGroundAnchor, jump.PlannedLanding, parameters, out JumpTrajectorySolution trajectory))
                return NavigationSegmentCommitResult.Rejected;
            JumpRouteSegment resolvedSegment = GroundJumpGeometry.CreateSegment(navigationWorld, trajectory, NavigationBodySize, parameters.SupportSnapDistance);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, resolvedSegment, navigation, out OneWayPlatformCollisionLease lease))
                return NavigationSegmentCommitResult.Rejected;

            BeginJump(trajectory, lease);
            return NavigationSegmentCommitResult.Committed;
        }

        /// <summary>Creates the Jump planner request from main-thread physics values.</summary>
        protected override NavigationPlanningOperation CreateNavigationPlanningOperation(
            MapNavigationRuntime navigation, Vector2 start, NavigationGoalRequest goalRequest,
            CancellationToken cancellationToken, NavigationPlanningPurpose purpose)
            => navigation.PlanJumpAsync(
                start,
                goalRequest,
                new JumpNavigationParameters(
                    NavigationBodySize,
                    Physics2D.gravity,
                    RigidBody.gravityScale,
                    RigidBody.linearDamping,
                    jumpHeight,
                    jumpLength,
                Time.fixedDeltaTime),
                cancellationToken,
                purpose);

        private JumpTrajectoryInput CreateTrajectoryInput(Vector2 start, Vector2 landing)
            => new(start, landing, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, Time.fixedDeltaTime);

        /// <summary>Advances the ballistic executor through stable landing; direct jumps have no route segment.</summary>
        protected override ExecutionResult TickCommittedNavigationTraversal(NavigationRouteSegment segment)
        {
            if (executor == null) return ExecutionResult.Completed;
            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            RecordStallFailure(result);
            if (result.Status != ExecutionStatus.Running) CleanupActiveJump();
            return result;
        }

        /// <summary>Starts a jump with the lease resolved from its immutable route segment.</summary>
        private void BeginJump(JumpTrajectorySolution trajectory, OneWayPlatformCollisionLease lease)
        {
            executor = new BallisticJumpExecutor(RigidBody, Collider, NavigationColliders, terrainFilter, trajectory, lease, MaximumIdleDuration);
            InvokeJumpCallback();
            jumpCountDown = EffectiveJumpInterval;
        }

        /// <summary>Releases one completed or unexpectedly landed jump and its collision lease.</summary>
        private void CleanupActiveJump()
        {
            executor?.Dispose();
            executor = null;
        }

        /// <summary>Reinitializes direct-jump progress measurement after an intentional pause.</summary>
        protected override void ResetTraversalProgressBaseline()
        {
            base.ResetTraversalProgressBaseline();
            executor?.ResetProgressBaseline();
        }

        private float EffectiveJumpInterval => jumpInterval / ValidateJumpCadence();

        private float ValidateJumpCadence()
        {
            float modifier = speedModifier;
            if (!NavigationNumeric.IsFinite(modifier) || modifier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speedModifier), modifier,
                    "Jump speedModifier must be finite and positive.");
            return modifier;
        }

        private void InvokeJumpCallback()
        {
            foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
            {
                MonoBehaviour callbackTarget = component;
                try { CallbackTable.Call(ref callbackTarget, JumpCallbackMethodName); }
                catch { }
            }
        }

        /// <summary>Completes ordinary jump navigation after releasing runtime state.</summary>
        protected override void FinishNavigation(bool success)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) MovementReplanDiagnostics.RecordMovementSuccess();
            else MovementReplanDiagnostics.RecordMovementFailure();
#endif
            if (success) CompleteSuccess();
            else CompleteFailure();
        }

        private void CompleteSuccess()
        {
            CleanupNewBackend();
            Success();
        }

        private void CompleteFailure()
        {
            CleanupNewBackend();
            Fail();
        }

        private void CompleteWithException(Exception exception)
        {
            CleanupNewBackend();
            Exception(exception);
        }

        private void CleanupNewBackend()
        {
            CleanupNavigationLifecycle();
            executor?.Dispose();
            executor = null;
        }

        /// <summary>Chooses a valid authored wander landing.</summary>
        protected override Vector2Int GetWanderLocation(Vector2 center)
        {
            const int MaximumTrials = 20;
            if (wanderDistance <= 0f) return Vector2Int.RoundToInt(center);

            for (int index = 0; index < MaximumTrials; index++)
            {
                var random = behaviourTree.RandomSources.Resolve(this);
                float x = random.NextFloat(-1f, 1f)
                    * random.NextFloat(wanderDistance * 0.5f, wanderDistance * 1.5f);
                Vector2Int candidate = Vector2Int.RoundToInt(center + Vector2.right * x);
                if (IsValidNavigationWanderLocation(candidate, true)) return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. Is the entity outside the room?");
            return Vector2Int.FloorToInt(center);
        }

        private bool IsOnGround()
        {
            LayerMask groundLayerMask = terrainFilter.layerMask;
            const int step = 5;
            Vector2 direction = Vector2.down;
            Vector2 start = Collider.bounds.min;
            float stepProgress = Collider.bounds.size.x / (step - 1);

            for (int i = 0; i < step; i++)
            {
                Vector2 center = start;
                center.x += stepProgress * i;
                RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, groundLayerMask);
                Debug.DrawRay(center, direction);
                if (hit.collider != null) return true;
            }
            return false;
        }

        /// <summary>Releases node-owned jump and planning state.</summary>
        public override void OnDestroy() => CleanupNewBackend();
    }
}
