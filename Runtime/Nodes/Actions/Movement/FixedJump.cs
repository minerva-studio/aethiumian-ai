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
        /// <summary>
        /// Attempts an unvalidated ballistic jump when strict solving finds no jump. Horizontal travel is
        /// clamped to jumpLength; an unreachable target height falls back to the launch height. The executor
        /// still checks the actual endpoint, which may differ from the requested target. Disabled by default.
        /// </summary>
        [Readable, Tooltip("If no validated jump exists, attempt one toward the target within jumpLength. Unreachable height uses launch height. Obstacles and landing support are not guaranteed; actual endpoint observation still determines success.")]
        public VariableField<bool> bestEffort = false;
        /// <summary>Allows an impulse from an unsupported launch pose. False retains the grounded launch contract for existing assets.</summary>
        [Readable, Tooltip("Allow launching from the current airborne pose. Disabled by default; launch support is required.")]
        public VariableField<bool> allowAirborneLaunch = false;
        /// <summary>Allows an unsupported endpoint, completed only after observing actual arrival. False retains physical landing for existing assets.</summary>
        [Readable, Tooltip("Allow an airborne endpoint. Actual arrival is still required. Disabled by default; landing support is required.")]
        public VariableField<bool> allowAirborneEndpoint = false;
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
            if (!FixedJumpTargetGeometry.TryResolve(target, targetMode, targetMeasurement, targetOffset, out capturedTargetBounds, out capturedLanding))
                CompleteAction(false);
        }

        protected override void OnMissingRuntime() => CompleteAction(false);

        protected override void InitializeAction() { }

        /// <summary>Waits for the shared physical launch prerequisite, then prepares one jump from the current pose.</summary>
        private void PrepareJump()
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

            NavigationGoalRequest completionGoal = FixedJumpTargetGeometry.CreateCompletionRequest(targetBounds, goal, distanceMetric, arrivalTolerance);
            // PlannedStep uses the same goal-center region gate as Simple Jump. Direct checks
            // its explicit lower-center endpoint; trajectory geometry stays lower-center based.
            Vector2 destination = targetMode == JumpTargetMode.Direct ? directLanding : completionGoal.TargetBounds.Center;
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

            if (!jumpParameters.AllowAirborneLaunch && !NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out _)) return;

            if (targetMode == JumpTargetMode.Direct)
            {
                if (!TryStartDirectJump(world, start, directLanding)) CompleteAction(false);
                return;
            }

            planningOperation = NavigationRuntime.PlanJumpAsync(startBody, completionGoal, jumpParameters, NavigationPlanningExtent.NextAction, ExecutionCancellation);
        }

        private bool IsReached(NavigationGoalRequest completionGoal, AABB body)
        {
            if (RigidBody.linearVelocityY > 0f || !NavigationWorld.IsGoalComplete(completionGoal, body)) return false;
            if (!jumpParameters.AllowAirborneEndpoint
                && (!NavigationRuntime.TryResolvePlanningGroundSupport(body, out _, out _)
                    || !NavigationWorldQueries.TryGetGroundSupportPoint(Collider, NavigationRuntime.CreateTerrainFilter(), out _))) return false;
            return true;
        }

        /// <summary>Advances planning or the committed single jump on the fixed-step path.</summary>
        protected override void TickAction()
        {
            if (executor == null && planningOperation == null)
            {
                PrepareJump();
                if (IsComplete || executor == null && planningOperation == null) return;
            }
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
                    Vector2 currentStart = NavigationBodyGeometry.GetMergedAabb(NavigationColliders).LowerCenter;
                    if (!bestEffort || !TryStartDirectJump(NavigationWorld, currentStart, capturedLanding))
                    {
                        CompleteAction(false);
                        return;
                    }
                }
                else if (route.Count == 0)
                {
                    AABB currentBody = NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
                    bool currentlyReached = IsReached(FixedJumpTargetGeometry.CreateCompletionRequest(capturedTargetBounds, goal, distanceMetric, GetArrivalTolerance()), currentBody);
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
                else
                {
                    if (route[0] is not JumpRouteSegment jump)
                    {
                        CompleteActionException(new InvalidOperationException("FixedJump PlannedStep requires a Jump route segment."));
                        return;
                    }

                    AABB plannedBody = NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
                    Vector2 plannedStart = plannedBody.LowerCenter;
                    if (!jumpParameters.AllowAirborneLaunch
                        && (!NavigationRuntime.TryResolvePlanningGroundSupport(plannedBody, out _, out NavigationSupport currentSupport)
                        || !NavigationRuntime.TryResolvePlanningGroundSupport(AABB.FromLowerCenter(jump.Start, bodySize), out _, out NavigationSupport plannedSupport)
                        || currentSupport.Surface != plannedSupport.Surface))
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

            parameters = new JumpNavigationParameters(Physics2D.gravity, RigidBody.gravityScale, RigidBody.linearDamping, height, length, Time.fixedDeltaTime, allowAirborneLaunch, allowAirborneEndpoint);
            return true;
        }

        /// <summary>Commits a strict solution, or an explicitly requested best-effort trajectory, to the same physical executor.</summary>
        private bool TryStartDirectJump(INavigationWorld world, Vector2 start, Vector2 landing)
        {
            GroundJumpParameters parameters = jumpParameters.GetJumpParameters(bodySize);
            if (!NavigationRuntime.TryGetJumpSolver(out GroundJumpSolver jumpSolver)) return false;
            if (!jumpSolver.TrySolve(start, landing, parameters, out JumpTrajectorySolution solved))
            {
                if (!bestEffort) return false;
                Vector2 endpoint = new(start.x + Mathf.Clamp(landing.x - start.x, -parameters.JumpLength, parameters.JumpLength), landing.y);
                JumpTrajectoryInput input = new(start, endpoint, parameters.Gravity, parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight, parameters.SimulationTimeStep);
                if (!JumpTrajectory.TrySolve(input, out solved))
                {
                    endpoint.y = start.y;
                    input = new JumpTrajectoryInput(start, endpoint, parameters.Gravity, parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight, parameters.SimulationTimeStep);
                    if (!JumpTrajectory.TrySolve(input, out solved)) return false;
                }
                // Best effort relaxes path validation, never the authored region boundary.
                if (!IsNavigationDestinationAllowed(AABB.FromLowerCenter(start, bodySize).Center, solved.LandingPosition)) return false;
            }

            JumpRouteSegment segment = GroundJumpGeometry.CreateSegment(world, solved, bodySize);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(Collider, segment, NavigationRuntime, out OneWayPlatformCollisionLease lease))
            {
                return false;
            }

            executor = new BallisticJumpExecutor(RigidBody,
                Collider,
                NavigationColliders,
                NavigationRuntime.CreateTerrainFilter(),
                solved,
                lease,
                allowAirborneLaunch: jumpParameters.AllowAirborneLaunch,
                allowAirborneEndpoint: jumpParameters.AllowAirborneEndpoint);
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

        private float GetArrivalTolerance() => reachDistance == null || !reachDistance.HasValue ? 0f : reachDistance.NumericValue;
    }
}
