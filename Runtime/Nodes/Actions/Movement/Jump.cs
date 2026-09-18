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
    /// <summary>Moves an entity toward a goal through planner-provided ballistic actions.</summary>
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

        [NonSerialized] private float nextJumpTime;
        private float EffectiveJumpInterval => jumpInterval / ValidateJumpCadence();
        protected override NavigationGoalRequest BuildGoal(AABB target, AABB body)
            => CreateGoal(target, NavigationGoalGeometry.Proximity);

        protected override bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation)
        {
            operation = null;
            if (purpose == NavigationPlanningPurpose.InitialRoute && !NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out _)) return false;
            operation = NavigationRuntime.PlanJumpAsync(body, goal, new JumpNavigationParameters(body.Size, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength, Time.fixedDeltaTime), extent, cancellation, purpose);
            return true;
        }

        protected override bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected)
        {
            connected = null;
            if (candidate.Count == 0 || candidate.Segments[0] is not JumpRouteSegment jump) return false;
            // Keep the receipt while contact is temporarily absent; preparation owns launch waiting.
            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out _))
            { connected = candidate; return true; }
            if (!NavigationRuntime.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport current)
                || !NavigationRuntime.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, body.Size), out _, out NavigationSupport launch)
                || current.Surface != launch.Surface) return false;
            connected = candidate;
            return true;
        }

        protected override bool IsGoalSatisfied(NavigationGoalRequest goal, AABB body, bool swept)
            => NavigationWorld.IsGoalComplete(goal, body) && RigidBody.linearVelocity.y <= 0f && NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out _);

        protected override bool TryRecover(ExecutionFailureReason reason, NavigationGoalRequest goal, AABB body) => reason == ExecutionFailureReason.Obstructed;

        protected override void Finish(bool success, NavigationGoalRequest? goal) { }

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

        protected override ActionPreparation PrepareExecutor(NavigationRouteSegment segment, AABB body, MovementExecutor reusable, out MovementExecutor prepared)
        {
            prepared = null;
            if (segment is not JumpRouteSegment jump)
                throw new InvalidOperationException("Jump planning produced a non-jump route segment.");
            if (reusable != null && ExecutionTime < nextJumpTime) return ActionPreparation.Waiting;

            MapNavigationRuntime navigation = RequireNavigationRuntime(nameof(Jump));
            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, navigation.CreateTerrainFilter(), out _))
                return ActionPreparation.Waiting;
            if (!navigation.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport currentSupport))
                return ActionPreparation.Waiting;
            if (!navigation.TryResolvePlanningGroundSupport(
                AABB.FromLowerCenter(jump.Start, body.Size), out _, out NavigationSupport launchSupport))
                return ActionPreparation.Unavailable;
            if (currentSupport.Surface != launchSupport.Surface)
                return ActionPreparation.Unavailable;
            INavigationWorld navigationWorld = NavigationWorld;
            if (!navigation.TryGetJumpSolver(out GroundJumpSolver jumpSolver))
                return ActionPreparation.Waiting;
            GroundJumpParameters parameters = new(body.Size, Physics2D.gravity,
                RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength,
                Time.fixedDeltaTime, NavigationWorldQueries.SupportSnapDistance,
                GroundTraversalEndpointPolicy.VerticalSupportTolerance);
            if (!jumpSolver.TrySolve(body.LowerCenter, jump.End, parameters, out JumpTrajectorySolution trajectory))
                return ActionPreparation.Unavailable;
            JumpRouteSegment resolvedSegment = GroundJumpGeometry.CreateSegment(navigationWorld, trajectory, body.Size, parameters.SupportSnapDistance);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, resolvedSegment, navigation, out OneWayPlatformCollisionLease lease))
                return ActionPreparation.Unavailable;

            prepared = CreateJump(trajectory, lease);
            return ActionPreparation.Ready;
        }

        private BallisticJumpExecutor CreateJump(JumpTrajectorySolution trajectory, OneWayPlatformCollisionLease lease)
        {
            BallisticJumpExecutor action = null;
            try
            {
                action = new BallisticJumpExecutor(RigidBody, Collider, NavigationColliders, NavigationRuntime.CreateTerrainFilter(), trajectory, lease, MaximumIdleDuration);
                InvokeJumpCallback();
                nextJumpTime = ExecutionTime + EffectiveJumpInterval;
                return action;
            }
            catch
            {
                if (action != null) action.Dispose();
                else lease?.Dispose();
                throw;
            }
        }

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

        protected override Vector2 GetWanderLocation(Vector2 center, AABB body)
        {
            const int MaximumTrials = 20;
            if (wanderDistance <= 0f) return center;

            for (int index = 0; index < MaximumTrials; index++)
            {
                var random = behaviourTree.RandomSources.Resolve(this);
                float x = random.NextFloat(-1f, 1f)
                    * random.NextFloat(wanderDistance * 0.5f, wanderDistance * 1.5f);
                Vector2 candidate = center + Vector2.right * x;
                if (IsValidNavigationWanderLocation(candidate, body, true)) return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. Is the entity outside the room?");
            return center;
        }
    }
}
