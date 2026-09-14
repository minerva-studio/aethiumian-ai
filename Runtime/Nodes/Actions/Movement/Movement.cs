#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif
using Aethiumian.AI.Attributes;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
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
        [NonSerialized] private Vector2? previousCenter;
        [NonSerialized] private Vector2? retryAnchor;
        [NonSerialized] private NavigationGoalRegion retryGoal;
        [NonSerialized] private int retries;
        [NonSerialized] private float executionTime;
        [NonSerialized] private RetreatMovementExecution retreat;
        private const int MaximumNoProgressAttempts = 3;

        /// <summary>The owned executor instance, exposed for live navigation inspection.</summary>
        public MovementExecutor Executor => executor;
        /// <summary>Gets the merged world-space AABB used by planning and arrival checks.</summary>
        public Bounds NavigationBounds => NavigationBodyGeometry.GetMergedBounds(NavigationColliders);
        /// <summary>Gets the lower-center anchor of the merged navigation body AABB.</summary>
        public Vector2 NavigationGroundAnchor => NavigationBodyGeometry.GetGroundAnchor(NavigationColliders);
        /// <summary>Gets the center anchor of the merged navigation body AABB.</summary>
        public Vector2 NavigationCenterAnchor => NavigationBodyGeometry.GetCenterAnchor(NavigationColliders);
        /// <summary>Gets the merged navigation body AABB size.</summary>
        public Vector2 NavigationBodySize => NavigationBounds.size;
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
        protected RetreatMovementExecution RetreatExecution => retreat;
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
            executor = null;
            previousCenter = null;
            retryAnchor = null;
            retryGoal = null;
            retries = 0;
            executionTime = 0f;
            wanderDestination = null;
            retreat = null;
        }

        protected sealed override void TickAction()
        {
            Bounds body = NavigationBounds;
            if (!TryReadTarget(out Bounds target, out GameObject targetObject)) { EndMovement(false, null); return; }
            NavigationGoalRequest goalRequest = BuildGoal(target, body, out Vector2 anchor);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(goalRequest, NavigationWorld);
            executionTime += Time.fixedDeltaTime;
            bool swept = previousCenter.HasValue && SameGoal(retryGoal, goal)
                && goal.SweptIsComplete(previousCenter.Value, body.center, body.size);
            if (goal.IsRetreat)
            {
                retreat ??= new RetreatMovementExecution(targetObject, MaxApproachDistance, path == PathMode.Smart ? 0f : MaximumIdleDuration);
                if (!retreat.BeginTick(targetObject, goal, body.center))
                { EndMovement(false, goal); return; }
            }
            RefreshRetryBaseline(goal, anchor);
            bool physicalFailure = false;
            bool faulted = false;
            try
            {
                ReceiveRoute(goal, anchor, body);
                if (IsComplete) return;
                if (ActiveSegment == null)
                {
                    if (IsGoalSatisfied(goal, body, swept)) { EndMovement(true, goal); return; }
                    if (!PrepareNextAction(goal, anchor, body))
                    {
                        if (!IsComplete) RequestNextRoute(goal, anchor, body);
                        return;
                    }
                }

                NavigationRouteSegment action = ActiveSegment;
                ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
                RecordStallFailure(result);
                if (result.Status == ExecutionStatus.Failed)
                {
                    physicalFailure = true;
                    CancelRequest();
                    route = null;
                    routeIndex = 0;
                    if (!TryRecover(result.FailureReason, goal, body) || !AllowRetry())
                        EndMovement(false, goal);
                    else RequestNextRoute(goal, anchor, body);
                    return;
                }
                if ((result.Status == ExecutionStatus.Completed || !IsIrreversible(action))
                    && IsGoalSatisfied(goal, body, swept))
                { EndMovement(true, goal); return; }
                if (result.Status == ExecutionStatus.Completed)
                {
                    routeIndex++;
                    ReceiveRoute(goal, anchor, body);
                    if (IsComplete) return;
                    if (!PrepareNextAction(goal, anchor, body) && !IsComplete)
                        RigidBody.linearVelocity = Vector2.zero;
                }
                // Planning can overlap execution, but a second physical action never ticks here.
                RequestNextRoute(goal, anchor, body);
            }
            catch
            {
                faulted = true;
                retreat?.DiscardPendingTick();
                throw;
            }
            finally
            {
                if (!IsComplete && !faulted)
                {
                    previousCenter = body.center;
                    if (retreat != null)
                    {
                        if (!retreat.FinalizeTick(body.center, body.size, Time.fixedDeltaTime))
                            EndMovement(false, goal);
                        else if (!physicalFailure && !IsIrreversible(ActiveSegment) && retreat.HasReachedGoal(body.center, body.size))
                            EndMovement(true, goal);
                    }
                }
            }
        }

        protected sealed override void ResetActionProgress()
        {
            previousCenter = null;
            executor?.ResetProgressBaseline();
            retreat?.InvalidateSample();
        }
        public sealed override void Update() { }
        public sealed override void LateUpdate() { }

        /// <summary>Creates this ability's geometric goal and planning anchor from the tick sample.</summary>
        protected abstract NavigationGoalRequest BuildGoal(Bounds target, Bounds body, out Vector2 anchor);
        /// <summary>False means temporary physical prerequisites are missing; true supplies a request.</summary>
        protected abstract bool TryRequestRoute(Vector2 start, NavigationGoalRegion goal, NavigationPlanningPurpose purpose, CancellationToken cancellation, out NavigationPlanningOperation operation);
        /// <summary>Returns a route reconnected to actual physics; performs no executor or lease mutation.</summary>
        protected abstract bool TryConnectRoute(NavigationRoute candidate, Bounds body, out NavigationRoute connected);
        /// <summary>Prepares one route action after its predecessor was cancelled or completed.</summary>
        protected abstract ActionPreparation PrepareExecutor(NavigationRouteSegment segment, Bounds body, MovementExecutor reusable, out MovementExecutor prepared);
        /// <summary>Confirms the entire objective, including ability-specific support requirements.</summary>
        protected abstract bool IsGoalSatisfied(NavigationGoalRegion goal, Bounds body, bool swept);
        /// <summary>Authorizes recovery after a normal physical failure; never ends the node itself.</summary>
        protected abstract bool TryRecover(ExecutionFailureReason reason, NavigationGoalRegion goal, Bounds body);
        /// <summary>Applies final physics effects. Failure may arrive before a target was available.</summary>
        protected abstract void Finish(bool success, NavigationGoalRegion goal);
        // /// <summary>
        // /// Applies capability-specific effects when a traversal completes but no successor is ready.
        // /// Called once for that completion, while the node remains active, after route acquisition
        // /// and before requesting further planning. The default implementation leaves physics unchanged.
        // /// </summary>
        // protected virtual void OnTraversalCompletedWithoutSuccessor() { }

        private void EndMovement(bool success, NavigationGoalRegion goal)
        {
            if (success && retreat != null && !retreat.FinalizeTick(NavigationCenterAnchor, NavigationBodySize, Time.fixedDeltaTime))
                success = false;
            if (success) Finish(true, goal);
            CompleteAction(success);
        }
        protected sealed override void OnActionCompleting(bool success)
        {
            if (!success)
            {
                retreat?.DiscardPendingTick();
                if (RigidBody) Finish(false, null);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) MovementReplanDiagnostics.RecordMovementSuccess();
            else MovementReplanDiagnostics.RecordMovementFailure();
#endif
        }
        protected sealed override void ReleaseActionResources()
        {
            try { CancelRequest(); }
            finally
            {
                try { executor?.Dispose(); }
                finally
                {
                    executor = null;
                    route = null;
                    routeIndex = 0;
                    previousCenter = null;
                    retryAnchor = null;
                    retryGoal = null;
                    wanderDestination = null;
                    retreat = null;
                }
            }
        }
        protected MapNavigationRuntime RequireNavigationRuntime(string caller)
            => NavigationRuntime != null && !NavigationRuntime.IsDisposed
                ? NavigationRuntime
                : throw new InvalidOperationException($"{caller} requires this execution's live runtime.");
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
