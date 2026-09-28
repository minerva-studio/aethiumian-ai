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
        private const int MAXIMUM_NO_PROGRESS_ATTEMPTS = 3;

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

        /// <summary>Maximum continuous time allowed without a new best traversal progress value; zero disables this timeout. The project must provide a finite non-negative value.</summary>
        [Readable] public VariableField<float> maxIdleDuration = 3f;

        /// <summary>Maximum cumulative approach distance allowed while retreating; zero means unlimited. The project must provide a finite non-negative value.</summary>
        [DisplayIf(nameof(type), Behaviour.Retreat)]
        [Readable] public VariableField<float> maxApproachDistance = 1f;

        [NonSerialized] private NavigationRoute route;
        [NonSerialized] private NavigationGoalRequest routeGoal;
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
        // Trace and Retreat bind object identity at execution start; their geometry remains live.
        [NonSerialized] private GameObject capturedTarget;


        /// <summary>The physics contact filter for navigation terrain queries in this run.</summary>
        protected ContactFilter2D TerrainFilter => NavigationRuntime.CreateTerrainFilter();

        /// <summary>The owned executor instance, exposed for live navigation inspection.</summary>
        public MovementExecutor Executor => executor;
        /// <summary>
        /// Gets the merged world-space AABB used by planning and arrival checks. This is the node's
        /// single body-pose contract: position and size are read from it, never recombined by callers.
        /// </summary>
        public AABB NavigationBodyAabb => NavigationBodyGeometry.GetMergedAabb(NavigationColliders);
        /// <summary>
        /// Gets the current route, whose HasValue is false before acquisition and after completion or cancellation.
        /// </summary>
        public NavigationRoute Route => route;
        /// <summary>
        /// Gets the index of the next segment to execute, which may be equal to Route.Count if the last segment was completed?
        /// /// </summary>
        public int RouteIndex => routeIndex;
        protected float ExecutionTime => executionTime;
        protected RetreatExecution RetreatExecution => retreat;
        protected NavigationPlanningExtent PlanningExtent => path == PathMode.Smart ? NavigationPlanningExtent.Route : NavigationPlanningExtent.NextAction;
        public NavigationRouteSegment ActiveSegment => executor != null && executor.IsExecuting && route.HasValue && routeIndex < route.Count ? route[routeIndex] : null;

        /// <inheritdoc/>
        protected sealed override bool RequiresNavigationWorld => path != PathMode.Naive;

        private bool NaiveConfigurationRejected => path == PathMode.Naive && (type == Behaviour.Retreat || goal == MovementGoal.Confront || goal == MovementGoal.FiringPosition);

        protected sealed override void InitializeAction()
        {
            if (NaiveConfigurationRejected)
                throw new InvalidOperationException("Naive movement cannot retreat or use line-of-sight goals.");
            route = default;
            routeGoal = default;
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
            capturedTarget = null;
            if ((type == Behaviour.Trace || type == Behaviour.Retreat) && tracing != null && tracing.HasValue)
            {
                capturedTarget = tracing.GameObjectValue;
            }
        }

        protected sealed override void TickAction()
        {
            AABB body = NavigationBodyAabb;
            if (!TryReadTarget(body, out AABB target, out GameObject targetObject))
            {
                EndMovement(false, null); return;
            }
            NavigationGoalRequest goal = BuildGoal(target);
            executionTime += Time.fixedDeltaTime;
            bool firstGoalSample = !intentGoal.HasValue;
            bool planningInvalidated = false;
            bool faulted = false;
            try
            {
                if (path != PathMode.Naive && !goal.IsRetreat)
                {
                    bool destinationAllowed = IsNavigationDestinationAllowed(body.Center, goal.TargetBounds.Center);
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
                    retreat ??= new RetreatExecution(targetObject, maxApproachDistance);
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
                    MaintainPlanning(goal, body, skipCountingThisTick: firstGoalSample || planningInvalidated || fallbackReceiptConsumed);
                    return;
                }

                NavigationRouteSegment action = ActiveSegment;
                ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
                ReportMovementStateAfterExecution(action, result);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (result.FailureReason == ExecutionFailureReason.Stalled) MovementReplanDiagnostics.RecordMovementStall();
#endif
                if (result.Status == ExecutionStatus.Failed)
                {
                    CancelPlanningRequests();
                    route = default;
                    routeGoal = default;
                    routeIndex = 0;
                    bool recoverable = path == PathMode.Naive
                        ? result.FailureReason == ExecutionFailureReason.Obstructed || result.FailureReason == ExecutionFailureReason.UnexpectedSupport
                        : TryRecover(result.FailureReason, body);
                    if (!recoverable || !AllowRetry())
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
                        RigidBody.linearVelocityX = 0;
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
        /// Creates this ability's geometric goal from the tick's target sample. Route planning receives
        /// the body AABB separately and derives its own mode-specific start anchor from that pose.
        /// </summary>
        protected abstract NavigationGoalRequest BuildGoal(AABB target);

        /// <summary>
        /// False means temporary physical prerequisites are missing; true supplies the requested planning horizon.
        /// </summary>
        protected abstract bool TryRequestRoute(AABB body, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation);

        /// <summary>Wraps an immediate direct route in the normal planning receipt contract.</summary>
        protected static NavigationPlanningOperation CompletedPlan(NavigationRoute route)
        {
            var operation = new NavigationPlanningOperation();
            operation.TryComplete(route.HasValue ? NavigationPlanResult.ResultProduced(route) : NavigationPlanResult.NoResult);
            return operation;
        }

        /// <summary>
        /// Returns a route reconnected to actual physics; performs no goal-policy, executor, or lease mutation.
        /// </summary>
        protected abstract bool TryConnectRoute(NavigationRoute candidate, AABB body, out NavigationRoute connected);

        /// <summary>
        /// Starts one route segment after its predecessor was cancelled or completed. An implementation may
        /// reconfigure and return the current <see cref="Executor"/> or return a new one; pending and rejected
        /// results must leave the current executor untouched. Movement disposes a replaced executor on adoption.
        /// </summary>
        protected abstract SegmentStartResult StartSegment(NavigationRouteSegment segment, AABB body);
        /// <summary>
        /// Confirms the movement goal. The default checks goal completion or swept completion; a capability
        /// may add physical requirements such as Jump landing support.
        /// </summary>
        protected virtual bool IsGoalSatisfied(NavigationGoalRequest goal, AABB body, bool swept) => NavigationWorld.IsGoalComplete(goal, body) || swept;

        /// <summary>
        /// Authorizes recovery after a normal physical failure; the default permits Obstructed failures.
        /// An override may add capability-specific recovery conditions and never ends the node itself.
        /// </summary>
        protected virtual bool TryRecover(ExecutionFailureReason reason, AABB body) => reason == ExecutionFailureReason.Obstructed;

        /// <summary>
        /// Applies final capability-specific physics effects.
        /// </summary>
        protected abstract void Finish(bool success);

        private bool RecordRetreatApproach(NavigationGoalRequest goal, AABB tickStartBody)
        {
            if (retreat == null || !goal.IsRetreat) return true;

            AABB tickEndBody = NavigationBodyAabb;
            float additionalApproachDistance = RetreatNavigationGeometry.SegmentApproachDistance(tickStartBody.Center, tickEndBody.Center, goal.TargetBounds.Center);
            return retreat.RecordApproachDistance(additionalApproachDistance);
        }

        /// <summary>
        /// Settles one terminal movement outcome. A failed run may have no sampled goal at all,
        /// for example when the target disappeared before the first permitted tick.
        /// </summary>
        private void EndMovement(bool success, NavigationGoalRequest? goal)
        {
            if (success && goal.HasValue) Finish(true);
            CompleteAction(success);
        }

        private void ReportMovementStateAfterExecution(NavigationRouteSegment action, ExecutionResult result)
        {
            if (action is JumpRouteSegment)
            {
                if (executor is GroundTraversalExecutor ground && ground.HasJumpLaunched)
                    ReportMovementState(MovementState.Jumping);
                return;
            }

            if (result.Status == ExecutionStatus.Failed) return;
            ReportMovementState(action switch
            {
                GroundRouteSegment => MovementState.Walking,
                JumpRouteSegment => MovementState.Jumping,
                FallRouteSegment => MovementState.Falling,
                DropThroughRouteSegment => MovementState.DroppingThrough,
                FlyRouteSegment => MovementState.Flying,
                _ => throw new InvalidOperationException(
                    $"Movement navigation produced an unsupported state for {action?.GetType().Name ?? "null"}.")
            });
        }

        protected sealed override void OnActionCompleting(bool success)
        {
            if (!success)
            {
                if (RigidBody) Finish(false);
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
                    route = default;
                    routeGoal = default;
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

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            if (NaiveConfigurationRejected)
            {
                Debug.LogError($"Naive {GetType().Name} cannot retreat or use line-of-sight goals.", tree?.prefab);
                return false;
            }
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
                Validate.NonNegativeFinite(maxApproachDistance, nameof(maxApproachDistance));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Invalid {GetType().Name} Retreat approach budget on {name}: {exception.Message}", gameObject);
                return false;
            }
        }


        /// <summary>
        /// Whether a route segment started, should be retried on a later fixed step, or rejects its candidate route.
        /// </summary>
        protected enum SegmentStartStatus
        {
            Pending,
            Started,
            Rejected
        }

        /// <summary>
        /// Outcome of starting one route segment, distinct from the executor's physical result.
        /// A started result always carries the executor now running the segment.
        /// </summary>
        protected readonly struct SegmentStartResult
        {
            public static SegmentStartResult Pending => default;
            public static SegmentStartResult Rejected => new(SegmentStartStatus.Rejected, null);

            public SegmentStartStatus Status { get; }
            public MovementExecutor Executor { get; }

            private SegmentStartResult(SegmentStartStatus status, MovementExecutor executor)
            {
                Status = status;
                Executor = executor;
            }

            public static SegmentStartResult Started(MovementExecutor executor) => new(SegmentStartStatus.Started, executor ?? throw new ArgumentNullException(nameof(executor)));
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
            Smart,
            [Tooltip("Move directly toward the target without querying the navigation world")]
            Naive
        }

    }
}
