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

        [NonSerialized] private MovementGoalProvider goalProvider;
        [NonSerialized] private RetreatMovementExecution retreatMovementExecution;
        [NonSerialized] private RollingNavigationSession navigationSession;
        /// <summary>Validated timeout used by the current execution owner.</summary>
        protected float MaximumIdleDuration => ValidateMaximumIdleDuration();

        [NonSerialized] private Vector2 previousNavigationCenter;
        /// <summary>Whether the shared sweep baseline is valid for the current traversal.</summary>
        [NonSerialized] protected bool hasPreviousNavigationCenter;
        /// <summary>
        /// is simple movement? (without pathfinder)
        /// </summary>
        public bool isBlind => path == PathMode.Simple;
        public bool isSmart => path == PathMode.Smart;

        /// <summary>Gets the merged world-space AABB used by planning and arrival checks.</summary>
        public Bounds NavigationBounds => NavigationBodyGeometry.GetMergedBounds(NavigationColliders);
        /// <summary>Gets the lower-center anchor of the merged navigation body AABB.</summary>
        public Vector2 NavigationGroundAnchor => NavigationBodyGeometry.GetGroundAnchor(NavigationColliders);
        /// <summary>Gets the center anchor of the merged navigation body AABB.</summary>
        public Vector2 NavigationCenterAnchor => NavigationBodyGeometry.GetCenterAnchor(NavigationColliders);
        /// <summary>Gets the merged navigation body AABB size.</summary>
        public Vector2 NavigationBodySize => NavigationBounds.size;
        /// <summary>Gets the per-execution goal provider without exposing a concrete provider implementation.</summary>
        protected MovementGoalProvider GoalProvider => goalProvider;
        /// <summary>Gets the Retreat execution state for capability-owned planning and completion.</summary>
        protected RetreatMovementExecution RetreatExecution => retreatMovementExecution;
        /// <summary>Gets the sole route owner for capability-driven advancement and recovery.</summary>
        public RollingNavigationSession Navigation => navigationSession;

        /// <summary>Gets the geometry used by an unqualified Default goal for this movement kind.</summary>
        protected virtual NavigationGoalGeometry DefaultGoalGeometry => NavigationGoalGeometry.Proximity;

        /// <summary>Gets the current world anchor used to splice and continue navigation routes.</summary>
        protected abstract Vector2 NavigationRequestAnchor { get; }

        /// <summary>Returns whether this policy intentionally completes after one committed route segment.</summary>
        protected virtual bool CompleteAfterOneNavigationSegment => false;

        /// <summary>Chooses the execution mechanism; Naive currently shares Simple policy.</summary>
        protected virtual bool UsesRouteExecution => isSmart && type != Behaviour.Wander;




        /// <summary>Builds an immediately executable direct route, when supported by this movement policy.</summary>
        public virtual bool TryCreateDirectNavigationRoute(NavigationGoalRegion goal, out NavigationRoute route)
        {
            route = null;
            return false;
        }

        /// <summary>Recognizes already-consumed reversible segments without ticking physics.</summary>
        protected virtual bool IsNavigationSegmentConsumed(NavigationRouteSegment segment) => false;

        /// <summary>
        /// Gives a movement policy one opportunity to reconnect a completed route to its
        /// physically observed anchor. The default rejects reconnection so irreversible and
        /// non-ground actions cannot be spliced by coordinate coincidence.
        /// </summary>
        protected virtual bool TryReconnectNavigationRoute(
            NavigationRoute route, Vector2 currentAnchor, out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = null;
            return false;
        }

        /// <summary>Returns whether this movement policy may finish after its executor reports completion.</summary>
        protected abstract bool IsNavigationGoalReached(
            NavigationGoalRegion goalRegion, NavigationRouteSegment segment, bool sweptGoal);

        public float MaxApproachDistance
        {
            get
            {
                if (maxApproachDistance == null || !maxApproachDistance.HasValue)
                    throw new ArgumentException("Retreat max approach distance is required.", nameof(maxApproachDistance));
                float value = maxApproachDistance.NumericValue;
                if (!NavigationNumeric.IsFinite(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(maxApproachDistance), value,
                        "Retreat max approach distance must be finite and non-negative.");
                return value;
            }
        }

        #region Lifecycle

        protected sealed override void InitializeAction()
        {
            navigationSession = new RollingNavigationSession(this);
            navigationSession.ResetExecution();
            hasPreviousNavigationCenter = false;
            goalProvider = CreateGoalProvider();
            goalProvider.Initialize();
            if (IsComplete) return;
            InitializeMovement();
            if (IsComplete) return;
            if (!GoalProvider.ValidateTarget()) { CompleteAction(false); return; }
            NavigationGoalRequest request = CreateNavigationGoalRequest();
            retreatMovementExecution = request.Geometry == NavigationGoalGeometry.Retreat
                ? new RetreatMovementExecution(this) : null;
            if (type == Behaviour.Retreat) _ = MaxApproachDistance;
            StartMovement();
        }

        protected sealed override void TickAction()
        {
            if (!GoalProvider.ValidateTarget()
                || retreatMovementExecution != null && !retreatMovementExecution.BeginTick())
            {
                CompleteNavigationFailurePolicy();
                return;
            }
            BeforeMovementTick();
            if (IsComplete) return;
            if (UsesRouteExecution) Navigation.Tick();
            else TickDirectMovement();
            if (!IsComplete && retreatMovementExecution != null)
            {
                if (!retreatMovementExecution.FinalizeTick()) CompleteNavigationFailurePolicy();
                else if (retreatMovementExecution.HasReachedGoal()) CompleteNavigationPolicy();
            }
        }

        protected sealed override void ResetActionProgress()
        {
            hasPreviousNavigationCenter = false;
            ResetTraversalProgressBaseline();
        }

        /// <summary>
        /// Do not use update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void Update() {  /*nothing*/  }

        /// <summary>
        /// Do not use late update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void LateUpdate() {  /*nothing*/  }

        #endregion

        #region Authoring

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

        #endregion

        #region Movement Lifecycle

        /// <summary>
        /// Initializes capability-owned runtime state for this execution.
        /// </summary>
        protected virtual void InitializeMovement() { }

        /// <summary>
        /// Performs optional capability startup after target validation and Retreat binding.
        /// </summary>
        protected virtual void StartMovement() { }

        /// <summary>
        /// Advances capability physics or the rolling session on the allowed fixed-update path.
        /// </summary>
        protected virtual void BeforeMovementTick() { }

        /// <summary>Advances the capability's direct policy without a rolling route.</summary>
        protected abstract void TickDirectMovement();

        #endregion

        #region Goals and Arrival

        /// <summary>
        /// get a valid wander location for the entity
        /// </summary>
        /// <returns></returns>
        protected abstract Vector2Int GetWanderLocation(Vector2 center);

        /// <summary>Captures the pure goal request on the main thread.</summary>
        protected virtual NavigationGoalRequest CreateNavigationGoalRequest() => goalProvider.CreateGoalRequest();

        /// <summary>Builds a region only after binding the captured request to the current immutable world.</summary>
        protected NavigationGoalRegion GetNavigationGoalRegion()
        {
            NavigationGoalRequest request = CreateNavigationGoalRequest();
            return NavigationGoalRegion.Bind(request, NavigationWorld);
        }

        /// <summary>Observes the current body center and tests one reversible goal sweep.</summary>
        protected bool ObserveNavigationSweep(NavigationGoalRegion goalRegion)
        {
            if (goalRegion == null) return false;
            Vector2 currentCenter = NavigationCenterAnchor;
            if (!hasPreviousNavigationCenter)
            {
                previousNavigationCenter = currentCenter;
                hasPreviousNavigationCenter = true;
                return false;
            }

            Vector2 previousCenter = previousNavigationCenter;
            previousNavigationCenter = currentCenter;
            return goalRegion.SweptIsComplete(previousCenter, currentCenter, NavigationBodySize);
        }

        #endregion

        #region Traversal Progress

        /// <summary>
        /// Resets the active traversal's progress baseline after an intentional pause.
        /// </summary>
        protected virtual void ResetTraversalProgressBaseline()
        {
            retreatMovementExecution?.InvalidateSample();
        }

        /// <summary>Records executor-owned timeout feedback without making a second timeout decision.</summary>
        protected static void RecordStallFailure(ExecutionResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (result.FailureReason == ExecutionFailureReason.Stalled)
                MovementReplanDiagnostics.RecordMovementStall();
#endif
        }

        /// <summary>Validates the Movement-owned traversal stall configuration at its owning boundary.</summary>
        private float ValidateMaximumIdleDuration()
        {
            float value = maxIdleDuration;
            if (!NavigationNumeric.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxIdleDuration), value,
                    "Movement maxIdleDuration must be finite and non-negative.");
            return value;
        }

        /// <summary>Whether this node requires progress away from a threat, rather than toward a steering point.</summary>
        protected virtual bool MonitorRetreatStall => false;

        #endregion

        #region Navigation Context

        /// <summary>Requires this execution's borrowed runtime without resolving a replacement.</summary>
        protected MapNavigationRuntime RequireNavigationRuntime(string coordinatorName)
        {
            if (NavigationRuntime == null || NavigationRuntime.IsDisposed)
            {
                throw new InvalidOperationException(
                    $"{coordinatorName} requires a live navigation runtime for this execution.");
            }

            return NavigationRuntime;
        }

        /// <summary>Validates a wander anchor against the current immutable world and optional support contract.</summary>
        protected bool IsValidNavigationWanderLocation(Vector2Int target, bool requireSupport)
        {
            INavigationWorld world = NavigationWorld;
            Vector2 targetFeet = target;
            if (!world.AreInSameRegion(NavigationGroundAnchor, targetFeet)) return false;
            Vector2 bodySize = NavigationBodySize;
            if (!world.IsBodyClear(new Rect(targetFeet.x - bodySize.x * 0.5f, targetFeet.y,
                bodySize.x, bodySize.y), 0f)) return false;
            return !requireSupport || world.TryResolveSupport(targetFeet, bodySize,
                NavigationWorldQueries.SupportSnapDistance, out _);
        }

        #endregion

        #region Route Planning and Selection

        /// <summary>
        /// Creates one traversal-specific request after base state has captured its identity.
        /// </summary>
        protected abstract NavigationPlanningOperation CreateNavigationPlanningOperation(
            MapNavigationRuntime navigation, Vector2 start, NavigationGoalRequest goalRequest,
            CancellationToken cancellationToken, NavigationPlanningPurpose purpose);

        /// <summary>
        /// Checks physical route feasibility. The session supplies a non-empty route and checks behaviour constraints.
        /// </summary>
        protected abstract bool TryValidateNavigationRoute(NavigationRoute route);

        /// <summary>
        /// Measures progress from an execution anchor to one immutable goal region.
        /// </summary>
        protected virtual float NavigationDistanceToGoal(NavigationGoalRegion goalRegion, Vector2 anchor) => goalRegion.DistanceToLowerCenterBody(anchor, NavigationBodySize);

        /// <summary>
        /// Selects the next route segment and cursor reached after that segment completes.
        /// </summary>
        protected virtual bool TrySelectNavigationSegment(NavigationRoute route, int routeIndex, out NavigationRouteSegment segment, out int nextRouteIndex)
        {
            if (route == null || routeIndex < 0 || routeIndex >= route.Count)
            {
                segment = null;
                nextRouteIndex = routeIndex;
                return false;
            }

            segment = route.Segments[routeIndex];
            nextRouteIndex = routeIndex + 1;
            return true;
        }

        #endregion

        #region Committed Traversal

        /// <summary>Attempts to commit one route segment from the current physical state.</summary>
        protected abstract NavigationSegmentCommitResult TryCommitNavigationSegment(NavigationRouteSegment segment);

        /// <summary>Advances the concrete executor and translates its feedback into a route coordination result.</summary>
        protected abstract ExecutionResult TickCommittedNavigationTraversal(NavigationRouteSegment segment);

        /// <summary>Returns whether the committed traversal cannot be replaced until it completes.</summary>
        protected virtual bool IsCommittedTraversalIrreversible(NavigationRouteSegment segment) => segment is JumpRouteSegment or FallRouteSegment or DropThroughRouteSegment;

        /// <summary>Returns whether a reversible committed traversal now moves away from the latest goal.</summary>
        protected virtual bool IsCommittedTraversalReversed(NavigationRouteSegment segment, NavigationGoalRegion latestGoalRegion)
        {
            if (segment is not GroundRouteSegment ground) return false;
            Vector2 movement = ground.End - ground.Start;
            Vector2 toGoal = latestGoalRegion.Center - NavigationGroundAnchor;
            return movement.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon
                && toGoal.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon
                && Vector2.Dot(movement, toGoal) < 0f;
        }

        /// <summary>Cancels one reversible committed traversal without clearing externally owned velocity.</summary>
        protected virtual void CancelCommittedTraversal() => hasPreviousNavigationCenter = false;

        /// <summary>Handles a physical landing that differs from the planned support.</summary>
        protected virtual void HandleUnexpectedNavigationLanding(NavigationGoalRegion goalRegion) => CompleteNavigationFailurePolicy();

        #endregion

        #region Completion and Cleanup

        /// <summary>Completes the traversal policy after ordinary navigation has reached its terminal result.</summary>
        protected void CompleteNavigationPolicy()
        {
            if (retreatMovementExecution != null && !retreatMovementExecution.FinalizeTick())
            {
                CompleteNavigationFailurePolicy();
                return;
            }
            FinishNavigation(true);
        }

        /// <summary>Completes the traversal policy after ordinary navigation has failed.</summary>
        protected void CompleteNavigationFailurePolicy()
        {
            retreatMovementExecution?.DiscardPendingTick();
            FinishNavigation(false);
        }

        /// <summary>Applies capability-owned velocity, cleanup and completion after Retreat settlement.</summary>
        protected abstract void FinishNavigation(bool success);

        /// <summary>Releases all rolling-route state owned by this Movement execution.</summary>
        protected sealed override void ReleaseActionResources()
        {
            try
            {
                navigationSession?.ResetNavigationPlanningRequest(true);
            }
            finally
            {
                try { ReleaseMovementResources(); }
                finally
                {
                    navigationSession?.ClearRoutes();
                    navigationSession?.ResetSubmissionHistory();
                    navigationSession = null;
                    hasPreviousNavigationCenter = false;
                    retreatMovementExecution = null;
                    goalProvider = null;
                }
            }
        }

        /// <summary>Releases only the capability's executor and local state.</summary>
        protected abstract void ReleaseMovementResources();

        /// <summary>Applies failure velocity policy, including runtime invalidation.</summary>
        protected virtual void StopFailedMovement() { }

        protected sealed override void OnActionCompleting(bool success)
        {
            if (!success)
            {
                retreatMovementExecution?.DiscardPendingTick();
                if (RigidBody) StopFailedMovement();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (success) MovementReplanDiagnostics.RecordMovementSuccess();
            else MovementReplanDiagnostics.RecordMovementFailure();
#endif
        }

        #endregion

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
            [Tooltip("Directly move toward the destination")]
            Simple,
            [Tooltip("Use path finder to calculate the precise path to go to the destination")]
            Smart
        }

        /// <summary>
        /// Describes whether one route segment was deferred, committed, or rejected.
        /// </summary>
        protected enum NavigationSegmentCommitResult
        {
            /// <summary>
            /// Navigation world is not ready to commit a segment, or any states that would be committed are not yet valid. The segment may be retried later.
            /// </summary>
            Deferred,
            /// <summary>
            /// The segment was committed and the traversal is now in progress.
            /// </summary>
            Committed,
            /// <summary>
            /// The segment was rejected.
            /// </summary>
            Rejected,
        }
    }
}
