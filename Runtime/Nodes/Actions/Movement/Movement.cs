#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif
using Aethiumian.AI.Attributes;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Moves the entity toward a configured target.")]
    /// <summary>
    /// Base class for all actions involving movement of entities
    /// </summary>
    [Serializable]
    public abstract partial class Movement : NavigationAction
    {
        private const int MaximumNoProgressAttempts = 3;



        public PathMode path;
        public Behaviour type;

        /// <summary>Defines the legal destination set for trace and fixed destinations.</summary>
        [DisplayIf(nameof(type), Behaviour.Trace, Behaviour.FixedDestination)]
        public MovementGoal goal = MovementGoal.Default;

        /// <summary>Defines the metric for Proximity and FiringPosition goals.</summary>
        [DisplayIf(nameof(goal), MovementGoal.Proximity, MovementGoal.FiringPosition)]
        public DistanceMetric distanceMetric = DistanceMetric.Euclidean;

        [Constraint(VariableType.UnityObject)]
        [DisplayIf(nameof(type), Behaviour.Trace, Behaviour.Retreat)]
        [Readable] public VariableField tracing;

        /// <summary>
        /// the fixed destination for fixedDestination Behavior
        /// </summary>
        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.FixedDestination)]
        [Readable] public VariableField destination;

        [DisplayIf(nameof(type), Behaviour.Wander)] public WanderMode wanderMode;

        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.Wander)]
        [DisplayIf(nameof(wanderMode), WanderMode.AbsoluteCentered)]
        [Readable] public VariableField centerOfWander;
        [DisplayIf(nameof(type), Behaviour.Wander)]
        [DisplayIf(nameof(wanderMode), WanderMode.AbsoluteCentered)]
        public Space centerSpace;

        [DisplayIf(nameof(type), Behaviour.Wander)] public VariableField<float> wanderDistance;

        /// <summary>Distance to the target; Approach completes at ≤ this value, Retreat at ≥ this value.</summary>
        [FormerlySerializedAs("arrivalErrorBound")]
        [Readable] public VariableField<float> reachDistance;

        /// <summary>Maximum continuous time allowed without a new best traversal progress value; zero disables this timeout.</summary>
        [Readable] public VariableField<float> maxIdleDuration = 3f;

        /// <summary>Maximum cumulative approach distance allowed while retreating; zero means unlimited.</summary>
        [DisplayIf(nameof(type), Behaviour.Retreat)]
        [Readable] public VariableField<float> maxApproachDistance = 1f;

        [NonSerialized] private NavigationRoute route;
        [NonSerialized] private int routeIndex;
        [NonSerialized] private MovementExecutor executor;
        [NonSerialized] private NavigationPlanningRequest request;
        [NonSerialized] private NavigationPlanningRequest fallbackRequest;
        [NonSerialized] private int fallbackBackoffLevel;
        [NonSerialized] private AABB? previousBody;
        [NonSerialized] private AABB? retryPlannerBody;
        // The accepted intent and the progress sample have different lifetimes. The former
        // remains stable while an irreversible segment carries historical route metadata;
        // the latter is refreshed when retry/sweep evidence is reset. A null value means no
        // goal has been sampled yet in this run.
        [NonSerialized] private NavigationGoalRequest? intentGoal;
        [NonSerialized] private NavigationGoalRequest? progressGoal;
        [NonSerialized] private int simpleWaitTicks;
        [NonSerialized] private int retries;
        [NonSerialized] private float executionTime;
        [NonSerialized] private RetreatExecution retreat;



        /// <summary>The owned executor instance, exposed for live navigation inspection.</summary>
        public MovementExecutor Executor => executor;
        /// <summary>
        /// Gets the merged world-space AABB used by planning and arrival checks. This is the node's
        /// single body-pose contract: position and size are read from it, never recombined by callers.
        /// </summary>
        public AABB NavigationBodyAabb => NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
        /// <summary>
        /// Gets the current route, which may be null if no route has been acquired or if the last route was completed or cancelled?
        /// </summary>
        public NavigationRoute Route => route;
        /// <summary>
        /// Gets the index of the next segment to execute, which may be equal to Route.Count if the last segment was completed?
        /// /// </summary>
        public int RouteIndex => routeIndex;
        protected float MaximumIdleDuration => ValidateMaximumIdleDuration();
        protected float ExecutionTime => executionTime;
        protected RetreatExecution RetreatExecution => retreat;
        protected NavigationPlanningExtent PlanningExtent => path == PathMode.Smart ? NavigationPlanningExtent.Route : NavigationPlanningExtent.NextAction;
        public NavigationRouteSegment ActiveSegment => executor != null && executor.IsExecuting && route != null && routeIndex < route.Count ? route.Segments[routeIndex] : null;
        private float MaxApproachDistance
        {
            get
            {
                float value = maxApproachDistance;
                Validate.NonNegativeFinite(value, nameof(maxApproachDistance));
                return value;
            }
        }

        protected sealed override void InitializeAction()
        {
            // No target is read here. The first permitted tick performs the same sampling
            // path as every later tick, including choosing Wander's destination lazily.
            route = null;
            routeIndex = 0;
            request = null;
            fallbackRequest = null;
            fallbackBackoffLevel = 0;
            executor = null;
            previousBody = null;
            retryPlannerBody = null;
            intentGoal = null;
            progressGoal = null;
            simpleWaitTicks = 0;
            retries = 0;
            executionTime = 0f;
            wanderDestination = null;
            retreat = null;
        }

        protected sealed override void TickAction()
        {
            AABB body = NavigationBodyAabb;
            if (!TryReadTarget(body, out AABB target, out GameObject targetObject))
            {
                EndMovement(false, null); return;
            }
            NavigationGoalRequest goal = BuildGoal(target, body);
            executionTime += Time.fixedDeltaTime;
            bool firstGoalSample = !intentGoal.HasValue;
            bool planningInvalidated = false;
            bool faulted = false;
            try
            {
                if (!goal.IsRetreat)
                {
                    bool destinationAllowed = IsNavigationDestinationAllowed(body, goal);
                    if (!destinationAllowed)
                    {
                        EndMovement(false, goal);
                        return;
                    }
                }

                // Keep semantic invalidation inside the existing cleanup boundary so request
                // cancellation cannot bypass the node's normal fault/finalization handling.
                planningInvalidated = RefreshPlanningIntent(goal);
                if (goal.IsRetreat)
                {
                    retreat ??= new RetreatExecution(targetObject, MaxApproachDistance);
                    if (!retreat.IsCurrentTarget(targetObject))
                    {
                        EndMovement(false, goal); return;
                    }
                }
                bool swept = !planningInvalidated
                    && previousBody.HasValue
                    && progressGoal.HasValue
                    && progressGoal.Value.IsReusableFor(goal)
                    && NavigationWorld.IsGoalCompleteAlong(goal, previousBody.Value, body);
                RefreshProgressBaseline(goal, body, planningInvalidated);
                bool fallbackReceiptConsumed = TryAcquireAction(goal, body);
                if (IsComplete) return;
                if (ActiveSegment == null)
                {
                    ReportMovementState(MovementState.Idle);
                    if (IsGoalSatisfied(goal, body, swept))
                    {
                        EndMovement(RecordRetreatApproach(goal, body), goal);
                        return;
                    }
                    RefreshPendingPlanningRequest(goal, body);
                    MaintainPlanning(goal, body,
                        skipCountingThisTick: firstGoalSample || planningInvalidated || fallbackReceiptConsumed);
                    return;
                }

                NavigationRouteSegment action = ActiveSegment;
                ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
                ReportMovementStateAfterExecution(action, result);
                RecordStallFailure(result);
                if (result.Status == ExecutionStatus.Failed)
                {
                    CancelPlanningRequests();
                    route = null;
                    routeIndex = 0;
                    if (!TryRecover(result.FailureReason, goal, body) || !AllowRetry())
                    {
                        EndMovement(false, goal);
                    }
                    else
                    {
                        ReportMovementState(MovementState.Idle);
                        MaintainPlanning(goal, body, skipCountingThisTick: true);
                    }
                    return;
                }
                if ((result.Status == ExecutionStatus.Completed || action.IsReversible) && IsGoalSatisfied(goal, body, swept))
                {
                    EndMovement(RecordRetreatApproach(goal, body), goal);
                    return;
                }
                if (result.Status == ExecutionStatus.Completed)
                {
                    routeIndex++;
                    fallbackReceiptConsumed |= TryAcquireAction(goal, body);
                    if (IsComplete) return;
                    if (ActiveSegment == null)
                    {
                        ReportMovementState(MovementState.Idle);
                        RigidBody.linearVelocity = Vector2.zero;
                    }
                }
                // Planning can overlap execution, but a second physical action never ticks here.
                RefreshPendingPlanningRequest(goal, body);
                if (!IsComplete)
                    MaintainPlanning(goal, body, skipCountingThisTick: firstGoalSample || planningInvalidated || fallbackReceiptConsumed);
            }
            catch
            {
                faulted = true;
                throw;
            }
            finally
            {
                if (!IsComplete && !faulted)
                {
                    previousBody = body;
                    if (!RecordRetreatApproach(goal, body))
                    {
                        EndMovement(false, goal);
                    }
                }
            }
        }

        protected sealed override void ResetActionProgress()
        {
            previousBody = null;
            executor?.ResetProgressBaseline();
        }
        public sealed override void Update() { }
        public sealed override void LateUpdate() { }

        /// <summary>
        /// Creates this ability's geometric goal from the tick sample. The body AABB carries the whole
        /// pose; a planner derives its own mode-specific start anchor from the same body it receives.
        /// </summary>
        protected abstract NavigationGoalRequest BuildGoal(AABB target, AABB body);

        /// <summary>
        /// False means temporary physical prerequisites are missing; true supplies the requested planning horizon.
        /// </summary>
        protected abstract bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation);

        /// <summary>
        /// Returns a route reconnected to actual physics; performs no goal-policy, executor, or lease mutation.
        /// </summary>
        protected abstract bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected);

        /// <summary>
        /// Prepares one route action after its predecessor was cancelled or completed.
        /// </summary>
        protected abstract ActionPreparation PrepareExecutor(NavigationRouteSegment segment, AABB body, MovementExecutor reusable, out MovementExecutor prepared);
        /// <summary>
        /// Confirms the entire objective, including ability-specific support requirements.
        /// </summary>
        protected abstract bool IsGoalSatisfied(NavigationGoalRequest goal, AABB body, bool swept);

        /// <summary>
        /// Authorizes recovery after a normal physical failure; never ends the node itself.
        /// </summary>
        protected abstract bool TryRecover(ExecutionFailureReason reason, NavigationGoalRequest goal, AABB body);

        /// <summary>
        /// Applies final physics effects. Failure may arrive before a target was available,
        /// so the goal is absent on that path.
        /// </summary>
        protected abstract void Finish(bool success, NavigationGoalRequest? goal);

        private bool RecordRetreatApproach(NavigationGoalRequest goal, AABB tickStartBody)
        {
            if (retreat == null || !goal.IsRetreat) return true;

            AABB tickEndBody = NavigationBodyAabb;
            float additionalApproachDistance = RetreatNavigationGeometry.SegmentApproachDistance(
                tickStartBody.Center,
                tickEndBody.Center,
                goal.TargetBounds.Center);
            return retreat.RecordApproachDistance(additionalApproachDistance);
        }

        /// <summary>
        /// Settles one terminal movement outcome. A failed run may have no sampled goal at all,
        /// for example when the target disappeared before the first permitted tick.
        /// </summary>
        private void EndMovement(bool success, NavigationGoalRequest? goal)
        {
            if (success && goal.HasValue) Finish(true, goal);
            CompleteAction(success);
        }

        private static MovementState GetMovementState(NavigationRouteSegment segment)
            => segment switch
            {
                GroundRouteSegment => MovementState.Walking,
                JumpRouteSegment => MovementState.Jumping,
                FallRouteSegment => MovementState.Falling,
                DropThroughRouteSegment => MovementState.DroppingThrough,
                FlyRouteSegment => MovementState.Flying,
                _ => throw new InvalidOperationException(
                    $"Movement navigation produced an unsupported state for {segment?.GetType().Name ?? "null"}.")
            };

        private void ReportMovementStateAfterExecution(NavigationRouteSegment action, ExecutionResult result)
        {
            if (action is JumpRouteSegment)
            {
                if (executor is GroundTraversalExecutor ground && ground.HasJumpLaunched)
                    ReportMovementState(MovementState.Jumping);
                return;
            }

            if (result.Status != ExecutionStatus.Failed)
                ReportMovementState(GetMovementState(action));
        }

        protected sealed override void OnActionCompleting(bool success)
        {
            if (!success)
            {
                if (RigidBody) Finish(false, null);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) MovementReplanDiagnostics.RecordMovementSuccess();
            else MovementReplanDiagnostics.RecordMovementFailure();
#endif
        }
        protected sealed override void ReleaseActionResources()
        {
            try { CancelPlanningRequests(); }
            finally
            {
                try { executor?.Dispose(); }
                finally
                {
                    executor = null;
                    route = null;
                    routeIndex = 0;
                    fallbackBackoffLevel = 0;
                    previousBody = null;
                    retryPlannerBody = null;
                    intentGoal = null;
                    progressGoal = null;
                    simpleWaitTicks = 0;
                    wanderDestination = null;
                    retreat = null;
                }
            }
        }

        protected MapNavigationRuntime RequireNavigationRuntime(string caller)
        {
            var runtime = NavigationRuntime;
            if (runtime == null || runtime.IsDisposed)
                throw new InvalidOperationException($"{caller} requires this execution's live runtime.");
            return runtime;
        }

        protected static void RecordStallFailure(ExecutionResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (result.FailureReason == ExecutionFailureReason.Stalled) MovementReplanDiagnostics.RecordMovementStall();
#endif
        }

        public override bool EditorCheck(BehaviourTreeData tree)
        {
#if UNITY_EDITOR
            if (!tree.prefab)
            {
                EditorGUILayout.HelpBox("Behaviour tree has no prefab assigned, cannot determine whether rigid body and collider exist in runtime", MessageType.Warning);
            }
            else if (!tree.prefab.TryGetComponent<Rigidbody2D>(out _))
            {
                EditorGUILayout.HelpBox("No Rigidbody2D component on given prefab", MessageType.Error);
                return false;
            }
            else if (!tree.prefab.TryGetComponent<Collider2D>(out _))
            {
                EditorGUILayout.HelpBox("No Collider2D component on given prefab", MessageType.Error);
                return false;
            }
#endif
            if (type != Behaviour.Retreat) return true;
            try
            {
                _ = MaxApproachDistance;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Invalid {GetType().Name} Retreat approach budget on {name}: {exception.Message}", gameObject);
                return false;
            }
        }

        private float ValidateMaximumIdleDuration()
        {
            float value = maxIdleDuration;
            if (!NavigationNumeric.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxIdleDuration), value, "Movement maxIdleDuration must be finite and non-negative.");
            return value;
        }



        /// <summary>
        /// Outcome of action acquisition, distinct from the executor's physical result.
        /// </summary>
        protected enum ActionPreparation
        {
            Waiting,
            Ready,
            Unavailable
        }

        public enum Behaviour
        {
            /// <summary> directly toward to a gameObject </summary>
            [Tooltip("Directly toward to a gameObject")]
            Trace,
            /// <summary> random destination around a center </summary>
            [Tooltip("Random destination around a center")]
            Wander,
            /// <summary> a fixed destination </summary>
            [Tooltip("A fixed destination")]
            FixedDestination,
            /// <summary>Move directly away from a traced target until the retreat distance is reached.</summary>
            Retreat,
        }

        public enum WanderMode
        {
            SelfCentered,
            AbsoluteCentered,
        }

        public enum PathMode
        {
            [Tooltip("Plan and execute one reachable step at a time")]
            Simple,
            [Tooltip("Use path finder to calculate the precise path to go to the destination")]
            Smart
        }

    }
}
