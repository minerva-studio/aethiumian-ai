using System;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Checks strict fixed-jump geometry without launching, acquiring collision leases, or reporting movement.
    /// Direct checks one complete jump; PlannedStep checks for a valid next jump or an already-reached goal,
    /// not a complete multi-jump route. Each synchronous query reads the current inputs and pose.
    /// An unavailable world returns false. The result is a snapshot, not a reservation for a later action.
    /// </summary>
    [Serializable]
    [NodeMenuPath("Conditions/Navigation")]
    [NodeTip("Check whether a strict Fixed Jump is possible without moving; PlannedStep checks the next jump only")]
    public sealed class CanFixedJump : Determine
    {
        [Readable] public VariableField<float> jumpHeight = 30f;
        [Readable] public VariableField<float> jumpLength = 3f;
        [Readable, Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject)]
        public VariableField target;
        /// <summary>Direct validates the requested endpoint; PlannedStep asks for the next feasible jump.</summary>
        public FixedJump.JumpTargetMode targetMode;
        /// <summary>Selects collider bounds or transform measurement for PlannedStep object targets.</summary>
        public FixedJump.TargetMeasurement targetMeasurement;
        [Readable] public VariableField<Vector2> offset = Vector2.zero;
        [Readable] public VariableField<float> reachDistance = 0f;
        /// <summary>Allows a query from an unsupported launch pose without relaxing path clearance.</summary>
        [Readable] public VariableField<bool> allowAirborneLaunch = false;
        /// <summary>Allows unsupported endpoints without relaxing path clearance.</summary>
        [Readable] public VariableField<bool> allowAirborneEndpoint = false;
        public MovementGoal goal = MovementGoal.Default;
        public DistanceMetric distanceMetric = DistanceMetric.Euclidean;
        public NavigationRegionPolicy regionPolicy = NavigationRegionPolicy.InRegion;

        /// <summary>
        /// Evaluates current strict feasibility on the calling thread without waiting or queuing work.
        /// PlannedStep uses the runtime's bounded single-step planner and shared trajectory cache.
        /// Movement permission is irrelevant; missing runtime, world, body, or valid inputs returns false.
        /// </summary>
        public override bool GetValue()
        {
            MapNavigationRuntime runtime = NavigationRuntimeContext.Current;
            if (runtime == null || !runtime.TryGetWorld(out INavigationWorld world)) return false;
            Rigidbody2D body = gameObject.GetComponent<Rigidbody2D>();
            Collider2D bodyCollider = gameObject.GetComponent<Collider2D>();
            if (!body || !bodyCollider || jumpHeight == null || jumpLength == null) return false;
            Collider2D[] colliders = NavigationBodyGeometry.GetColliders(body);
            if (colliders.Length == 0) return false;

            float height = jumpHeight;
            float length = jumpLength;
            float tolerance = reachDistance == null || !reachDistance.HasValue ? 0f : reachDistance.NumericValue;
            Vector2 targetOffset = offset == null || !offset.HasValue ? Vector2.zero : offset.Vector2Value;
            if (!NavigationNumeric.IsFinite(height) || height <= 0f || !NavigationNumeric.IsFinite(length) || length < 0f
                || !NavigationNumeric.IsFinite(tolerance) || tolerance < 0f || !NavigationNumeric.IsFinite(targetOffset)
                || !Enum.IsDefined(typeof(FixedJump.JumpTargetMode), targetMode)
                || !Enum.IsDefined(typeof(FixedJump.TargetMeasurement), targetMeasurement)
                || !Enum.IsDefined(typeof(MovementGoal), goal) || !Enum.IsDefined(typeof(DistanceMetric), distanceMetric)
                || !FixedJumpTargetGeometry.TryResolve(target, targetMode, targetMeasurement, targetOffset, out AABB bounds, out Vector2 landing))
                return false;

            JumpNavigationParameters parameters = new(Physics2D.gravity, body.gravityScale, body.linearDamping, height, length, Time.fixedDeltaTime, allowAirborneLaunch, allowAirborneEndpoint);
            NavigationGoalRequest completionGoal = FixedJumpTargetGeometry.CreateCompletionRequest(bounds, goal, distanceMetric, tolerance);
            AABB startBody = NavigationBodyGeometry.GetMergedAabb(colliders);
            Vector2 destination = targetMode == FixedJump.JumpTargetMode.Direct ? landing : completionGoal.TargetBounds.Center;
            bool allowed = world.WorldBounds.Contains(startBody.Center) && world.WorldBounds.Contains(destination)
                && (regionPolicy == NavigationRegionPolicy.CrossRegion
                    || regionPolicy == NavigationRegionPolicy.InRegion && world.AreInSameRegion(startBody.Center, destination));
            if (!allowed || !NavigationNumeric.IsFinite(startBody.Min) || !NavigationNumeric.IsFinite(startBody.Max)
                || startBody.Size.x <= 0f || startBody.Size.y <= 0f
                || !parameters.AllowAirborneLaunch && !NavigationWorldQueries.TryGetGroundSupportPoint(bodyCollider, runtime.CreateTerrainFilter(), out _))
                return false;

            if (targetMode == FixedJump.JumpTargetMode.Direct)
                return runtime.TryGetJumpSolver(out GroundJumpSolver solver)
                    && solver.TrySolve(startBody.LowerCenter, landing, parameters.GetJumpParameters(startBody.Size), out _);

            if (!world.CanBodyPossiblyReachGoal(startBody.Size, completionGoal)) return false;
            return runtime.GetPlanner<JumpNavigationPlanner>().PlanSingleStep(startBody, completionGoal, parameters).Route.HasValue;
        }
    }
}
