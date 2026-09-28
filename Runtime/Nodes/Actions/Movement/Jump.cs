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
        [Header("Jump Property")]
        /// <summary>Maximum apex displacement above the launch support; the solver selects a lower arc when possible.</summary>
        [Readable] public VariableField<float> jumpHeight = 3f;
        [Readable] public VariableField<float> jumpLength = 3f;
        [Readable] public VariableField<float> jumpInterval = 1.5f;
        /// <summary>Scales only the interval between launches; the project must provide a finite positive value.</summary>
        [Readable] public VariableField<float> speedModifier = 1f;

        [NonSerialized] private float nextJumpTime;

        protected override NavigationGoalRequest BuildGoal(AABB target) => CreateGoal(target, NavigationGoalGeometry.Proximity);

        protected override bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation)
        {
            operation = null;
            if (path == PathMode.Naive)
            {
                if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out _)) return false;
                Vector2 start = body.LowerCenter;
                Vector2 landing = new(start.x + Mathf.Clamp(goal.TargetBounds.CenterX - start.x, -jumpLength, jumpLength), goal.TargetBounds.LowerCenter.y);
                JumpTrajectoryInput input = new(start, landing, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, Time.fixedDeltaTime);
                bool solvable = JumpTrajectory.TrySolve(input, out _);
                if (!solvable)
                {
                    landing.y = start.y;
                    input = new JumpTrajectoryInput(start, landing, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, Time.fixedDeltaTime);
                    solvable = JumpTrajectory.TrySolve(input, out _);
                }
                NavigationRoute route = default;
                if (solvable && Vector2.Distance(start, landing) > NavigationWorldQueries.GeometryEpsilon)
                {
                    bool reachesGoal = Vector2.Distance(landing, goal.TargetBounds.LowerCenter) <= NavigationWorldQueries.GeometryEpsilon;
                    route = NavigationRoute.Create(new[] { new JumpRouteSegment(start, landing) }, reachesGoal);
                }
                operation = CompletedPlan(route);
                return true;
            }
            if (purpose == NavigationPlanningPurpose.InitialRoute && !NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out _)) return false;
            JumpNavigationParameters parameters = new(Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength, Time.fixedDeltaTime);
            operation = NavigationRuntime.PlanJumpAsync(body, goal, parameters, extent, cancellation);
            return true;
        }

        protected override bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected)
        {
            connected = default;
            if (candidate.Count == 0 || candidate[0] is not JumpRouteSegment jump) return false;
            // Keep the receipt while contact is temporarily absent; preparation owns launch waiting.
            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out _))
            { connected = candidate; return true; }
            if (!NavigationRuntime.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport current)
                || !NavigationRuntime.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, body.Size), out _, out NavigationSupport launch)
                || current.Surface != launch.Surface) return false;
            connected = candidate;
            return true;
        }

        protected override bool IsGoalSatisfied(NavigationGoalRequest goal, AABB body, bool swept) => NavigationWorld.IsGoalComplete(goal, body) && RigidBody.linearVelocity.y <= 0f && NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out _);

        protected override void Finish(bool success) { }

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            if (!base.EditorCheck(tree)) return false;
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

        protected override SegmentStartResult StartSegment(NavigationRouteSegment segment, AABB body)
        {
            if (segment is not JumpRouteSegment jump)
                throw new InvalidOperationException("Jump planning produced a non-jump route segment.");
            if (Executor != null && ExecutionTime < nextJumpTime) return SegmentStartResult.Pending;

            if (!NavigationWorldQueries.TryGetGroundSupportPoint(Collider, TerrainFilter, out _))
                return SegmentStartResult.Pending;
            if (path == PathMode.Naive)
            {
                JumpTrajectoryInput input = new(body.LowerCenter, jump.End, Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, Time.fixedDeltaTime);
                if (!JumpTrajectory.TrySolve(input, out JumpTrajectorySolution direct))
                    return SegmentStartResult.Rejected;
                return SegmentStartResult.Started(CreateJump(direct, null));
            }
            MapNavigationRuntime navigation = NavigationRuntime;
            if (!navigation.TryResolvePlanningGroundSupport(body, out _, out NavigationSupport currentSupport))
                return SegmentStartResult.Pending;
            if (!navigation.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, body.Size), out _, out NavigationSupport launchSupport))
                return SegmentStartResult.Rejected;
            if (currentSupport.Surface != launchSupport.Surface)
                return SegmentStartResult.Rejected;
            INavigationWorld navigationWorld = NavigationWorld;
            if (!navigation.TryGetJumpSolver(out GroundJumpSolver jumpSolver))
                return SegmentStartResult.Pending;
            GroundJumpParameters parameters = new JumpNavigationParameters(Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, jumpHeight, jumpLength, Time.fixedDeltaTime).GetJumpParameters(body.Size);
            if (!jumpSolver.TrySolve(body.LowerCenter, jump.End, parameters, out JumpTrajectorySolution trajectory))
                return SegmentStartResult.Rejected;
            JumpRouteSegment resolvedSegment = GroundJumpGeometry.CreateSegment(navigationWorld, trajectory, body.Size);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, resolvedSegment, navigation, out OneWayPlatformCollisionLease lease))
                return SegmentStartResult.Rejected;

            return SegmentStartResult.Started(CreateJump(trajectory, lease));
        }

        private BallisticJumpExecutor CreateJump(JumpTrajectorySolution trajectory, OneWayPlatformCollisionLease lease)
        {
            BallisticJumpExecutor action = null;
            try
            {
                action = new BallisticJumpExecutor(RigidBody, Collider, NavigationColliders, TerrainFilter, trajectory, lease, maxIdleDuration);
                ReportMovementState(MovementState.Jumping);
                nextJumpTime = ExecutionTime + jumpInterval / speedModifier;
                return action;
            }
            catch
            {
                if (action != null) action.Dispose();
                else lease?.Dispose();
                throw;
            }
        }

        protected override Vector2 GetWanderLocation(Vector2 center, AABB body)
        {
            const int MAXIMUM_WANDER_LOCATION_TRIALS = 20;
            if (wanderDistance <= 0f) return center;

            for (int index = 0; index < MAXIMUM_WANDER_LOCATION_TRIALS; index++)
            {
                var random = behaviourTree.RandomSources.Resolve(this);
                float x = random.NextFloat(-1f, 1f)
                    * random.NextFloat(wanderDistance * 0.5f, wanderDistance * 1.5f);
                Vector2 candidate = center + Vector2.right * x;
                if (IsWanderCandidateAllowed(body.Center, candidate, AABB.FromLowerCenter(candidate, body.Size), true)) return candidate;
            }
            Debug.LogWarning("Cannot find valid wander location around. Is the entity outside the room?");
            return center;
        }
    }
}
