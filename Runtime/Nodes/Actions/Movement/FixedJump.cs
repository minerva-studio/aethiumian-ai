using Aethiumian.AI.Attributes;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Performs one bounded ballistic jump to a direct or planned-step target.</summary>
    [NodeTip("Perform one fixed ballistic jump to a direct or planned target")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Aethiumian.AI.Nodes", "Library-of-Meialia-AI")]
    public class FixedJump : NavigationAction
    {
        public enum JumpTargetMode
        {
            Direct = 0,
            PlannedStep = 1,
        }

        /// <summary>Defines how a PlannedStep target is converted into navigation geometry.</summary>
        public enum TargetMeasurement
        {
            ColliderBounds = 0,
            TransformPosition = 1,
        }

        [Numeric]
        [Readable]
        public VariableField jumpHeight = new(30f);
        [Numeric]
        [Readable]
        public VariableField<float> jumpLength = 3f;
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject)]
        [Readable]
        public VariableField target;
        public JumpTargetMode targetMode;
        /// <summary>Allows this action to finish without launching when its target is already reached.</summary>
        [Readable]
        public VariableField<bool> skipReached = false;
        /// <summary>Selects collider AABB or transform-position measurement for PlannedStep.</summary>
        public TargetMeasurement targetMeasurement;
        /// <summary>Defines the navigation region relationship used for target completion.</summary>
        public MovementGoal goal = MovementGoal.Default;
        /// <summary>Selects the distance metric for proximity-based completion goals.</summary>
        public DistanceMetric distanceMetric = DistanceMetric.Euclidean;
        [Readable]
        /// <summary>Sets the accepted navigation reach distance for the reached-target check.</summary>
        [FormerlySerializedAs("arrivalErrorBound")]
        public VariableField<float> reachDistance;
        [Readable]
        public VariableField<Vector2> offset = Vector2.zero;

        [NonSerialized] private AABB capturedTargetBounds;
        [NonSerialized] private Vector2 capturedLanding;
        [NonSerialized] private NavigationPlanningOperation planningOperation;
        [NonSerialized] private BallisticJumpExecutor executor;
        [NonSerialized] private JumpNavigationParameters jumpParameters;
        [NonSerialized] private Vector2 bodySize;

        /// <summary>Captures the target and prepares exactly one jump action.</summary>
        protected override void CaptureActionInput()
        {
            bodySize = NavigationBodyGeometry.GetWorldAabbSize(NavigationColliders);
            if (!NavigationNumeric.IsFinite(bodySize) || bodySize.x <= 0f || bodySize.y <= 0f)
            { CompleteAction(false); return; }
            if (!TryReadParameters(out jumpParameters, out Vector2 targetOffset)) return;
            if (!Enum.IsDefined(typeof(JumpTargetMode), targetMode) || !Enum.IsDefined(typeof(TargetMeasurement), targetMeasurement) || !Enum.IsDefined(typeof(MovementGoal), goal) || !Enum.IsDefined(typeof(DistanceMetric), distanceMetric))
            {
                CompleteAction(false);
                return;
            }
            TryResolveTarget(targetOffset, out capturedTargetBounds, out capturedLanding);
        }

        protected override void OnMissingRuntime() => CompleteAction(false);

        protected override void InitializeAction()
        {
            INavigationWorld world = NavigationWorld;
            AABB targetBounds = capturedTargetBounds;
            Vector2 directLanding = capturedLanding;

            AABB startBody = NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
            Vector2 start = startBody.LowerCenter;
            if (!NavigationNumeric.IsFinite(start))
            {
                CompleteAction(false);
                return;
            }
            float arrivalTolerance = GetArrivalTolerance();
            if (!NavigationNumeric.IsFinite(arrivalTolerance) || arrivalTolerance < 0f)
            {
                CompleteAction(false);
                return;
            }

            NavigationGoalRequest completionGoal = CreateCompletionRequest(targetBounds, arrivalTolerance);
            // Both jump modes check a ground-anchor destination: Direct uses its resolved landing
            // point, while PlannedStep uses the target's lower center. The region gate takes the body
            // center, matching every other NavigationAction; the jump itself stays lower-center based.
            Vector2 destination = targetMode == JumpTargetMode.Direct ? directLanding : completionGoal.TargetBounds.LowerCenter;
            if (!IsNavigationDestinationAllowed(startBody.Center, destination))
            {
                CompleteAction(false);
                return;
            }

            if (!NavigationNumeric.IsFinite(startBody.Min) || !NavigationNumeric.IsFinite(startBody.Max))
            {
                CompleteAction(false);
                return;
            }

            if (skipReached && IsReached(completionGoal, startBody))
            {
                CompleteAction(true);
                return;
            }

            if (targetMode == JumpTargetMode.Direct)
            {
                if (!TryStartDirectJump(world, start, directLanding)) CompleteAction(false);
                return;
            }

            if (IsReached(completionGoal, startBody))
            {
                // PlannedStep is still an action command when skipReached is false. Use the
                // current support as the landing so this does not enter the global search graph.
                if (!TryStartDirectJump(world, start, start)) CompleteAction(false);
                return;
            }

            planningOperation = NavigationRuntime.PlanJumpAsync(startBody, completionGoal, jumpParameters, NavigationPlanningExtent.NextAction, ExecutionCancellation);
        }

        private NavigationGoalRequest CreateCompletionRequest(AABB targetBounds, float arrivalTolerance)
        {
            bool requiresLineOfSight = goal == MovementGoal.Confront || goal == MovementGoal.FiringPosition;
            return goal == MovementGoal.Confront
                ? NavigationGoalRequest.GroundRange(targetBounds, arrivalTolerance, true)
                : NavigationGoalRequest.Proximity(targetBounds, distanceMetric, arrivalTolerance, requiresLineOfSight);
        }

        private bool IsReached(NavigationGoalRequest completionGoal, AABB body)
        {
            if (!NavigationRuntime.TryResolvePlanningGroundSupport(body, out _, out _)) return false;
            return NavigationWorld.IsGoalComplete(completionGoal, body);
        }

        /// <summary>Advances planning or the committed single jump on the fixed-step path.</summary>
        protected override void TickAction()
        {
            if (planningOperation != null)
            {
                if (!planningOperation.IsCompleted) return;
                NavigationPlanningOperation completed = planningOperation;
                planningOperation = null;
                if (completed.Exception != null)
                {
                    CompleteActionException(completed.Exception);
                    return;
                }

                NavigationRoute route = completed.Result;
                if (!route.HasValue)
                {
                    CompleteAction(false);
                    return;
                }

                if (route.Count == 0)
                {
                    AABB currentBody = NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
                    bool currentlyReached = IsReached(CreateCompletionRequest(capturedTargetBounds, GetArrivalTolerance()), currentBody);
                    if (skipReached && currentlyReached)
                    {
                        CompleteAction(true);
                        return;
                    }

                    // A single-step planner may legitimately report an empty route when
                    // the target became reached while the request was in flight. FixedJump
                    // remains an explicit action unless skipReached was requested, so launch
                    // one in-place jump from the current grounded support.
                    if (targetMode == JumpTargetMode.PlannedStep
                        && TryStartDirectJump(NavigationWorld, currentBody.LowerCenter, currentBody.LowerCenter))
                        return;

                    CompleteAction(false);
                    return;
                }

                if (route[0] is not JumpRouteSegment jump)
                {
                    CompleteActionException(new InvalidOperationException("FixedJump PlannedStep requires a Jump route segment."));
                    return;
                }

                AABB plannedBody = NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
                Vector2 plannedStart = plannedBody.LowerCenter;
                if (!NavigationRuntime.TryResolvePlanningGroundSupport(plannedBody, out _, out NavigationSupport currentSupport)
                    || !NavigationRuntime.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, bodySize), out _, out NavigationSupport plannedSupport)
                    || currentSupport.Surface != plannedSupport.Surface)
                {
                    CompleteAction(false);
                    return;
                }
                if (!TryStartDirectJump(NavigationWorld, plannedStart, jump.End))
                {
                    CompleteAction(false);
                    return;
                }
            }

            if (executor == null)
            {
                CompleteAction(false);
                return;
            }

            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            if (result.Status == ExecutionStatus.Completed) CompleteAction(true);
            else if (result.Status == ExecutionStatus.Failed) CompleteAction(false);
        }

        /// <summary>Resets the executor's progress baseline when execution is paused.</summary>
        protected override void ResetActionProgress() => executor?.ResetProgressBaseline();

        private bool TryReadParameters(out JumpNavigationParameters parameters, out Vector2 targetOffset)
        {
            parameters = default;
            targetOffset = Vector2.zero;
            if (jumpHeight == null || jumpLength == null)
            {
                CompleteAction(false);
                return false;
            }

            float height = jumpHeight.NumericValue;
            float length = jumpLength.NumericValue;
            targetOffset = offset == null || !offset.HasValue ? Vector2.zero : offset.Vector2Value;
            if (!NavigationNumeric.IsFinite(height) || height <= 0f || !NavigationNumeric.IsFinite(length) || length < 0f || !NavigationNumeric.IsFinite(targetOffset))
            {
                CompleteAction(false);
                return false;
            }

            parameters = new JumpNavigationParameters(Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, height, length, Time.fixedDeltaTime);
            return true;
        }

        private bool TryResolveTarget(Vector2 targetOffset, out AABB targetBounds, out Vector2 directLanding)
        {
            targetBounds = default;
            directLanding = default;
            if (target == null || target.IsNull)
            {
                CompleteAction(false);
                return false;
            }

            if (target.IsVector)
            {
                Vector2 point = target.Vector2Value + targetOffset;
                if (!NavigationNumeric.IsFinite(point))
                {
                    CompleteAction(false);
                    return false;
                }

                targetBounds = AABB.Point(point);
                directLanding = point;
                return true;
            }

            if (!target.IsFromGameObject)
            {
                CompleteAction(false);
                return false;
            }

            if (targetMode == JumpTargetMode.PlannedStep
                && targetMeasurement == TargetMeasurement.TransformPosition)
            {
                Vector2 point = (Vector2)target.PositionValue + targetOffset;
                if (!NavigationNumeric.IsFinite(point))
                {
                    CompleteAction(false);
                    return false;
                }

                targetBounds = AABB.Point(point);
                directLanding = point;
                return true;
            }

            Collider2D[] targetColliders = NavigationBodyGeometry.GetTargetColliders(target.GameObjectValue);
            if (targetColliders == null || targetColliders.Length == 0)
            {
                CompleteAction(false);
                return false;
            }

            targetBounds = NavigationBodyGeometry.GetMergedAabb(targetColliders);
            targetBounds = targetBounds.Translate(targetOffset);
            directLanding = targetBounds.LowerCenter;
            if (!NavigationNumeric.IsFinite(directLanding)
                || !NavigationNumeric.IsFinite(targetBounds.Min)
                || !NavigationNumeric.IsFinite(targetBounds.Max))
            {
                CompleteAction(false);
                return false;
            }

            return true;
        }

        private bool TryStartDirectJump(INavigationWorld world, Vector2 start, Vector2 landing)
        {
            GroundJumpParameters parameters = jumpParameters.GetJumpParameters(bodySize);
            if (!NavigationRuntime.TryGetJumpSolver(out GroundJumpSolver jumpSolver)
                || !jumpSolver.TrySolve(start, landing, parameters, out JumpTrajectorySolution solved))
            {
                return false;
            }

            JumpRouteSegment segment = GroundJumpGeometry.CreateSegment(world, solved, bodySize);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, segment, NavigationRuntime, out OneWayPlatformCollisionLease lease))
            {
                return false;
            }

            executor = new BallisticJumpExecutor(RigidBody, Collider, NavigationColliders, NavigationRuntime.CreateTerrainFilter(), solved, lease);
            ReportMovementState(MovementState.Jumping);
            return true;
        }

        protected override void ReleaseActionResources()
        {
            try { executor?.Dispose(); }
            finally
            {
                executor = null;
                planningOperation = null;
                capturedTargetBounds = default;
                capturedLanding = default;
            }
        }

        private float GetArrivalTolerance()
            => reachDistance == null || !reachDistance.HasValue ? 0f : reachDistance.NumericValue;

    }
}
