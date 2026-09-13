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
    public class FixedJump : Action
    {
        private const string JumpCallbackMethodName = "OnJump";

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
        /// <summary>Selects collider AABB or transform-position measurement for PlannedStep.</summary>
        public TargetMeasurement targetMeasurement;
        [DisplayIf(nameof(targetMode), JumpTargetMode.PlannedStep)]
        /// <summary>Defines the navigation region relationship used by PlannedStep.</summary>
        public MovementGoal goal = MovementGoal.Default;
        [DisplayIf(nameof(goal), MovementGoal.Proximity, MovementGoal.FiringPosition)]
        /// <summary>Selects the distance metric for Proximity and FiringPosition goals.</summary>
        public DistanceMetric distanceMetric = DistanceMetric.Euclidean;
        [DisplayIf(nameof(targetMode), JumpTargetMode.PlannedStep)]
        [Readable]
        /// <summary>Sets the accepted navigation reach distance for PlannedStep.</summary>
        [FormerlySerializedAs("arrivalErrorBound")]
        public VariableField<float> reachDistance;
        [Readable]
        public VariableField<Vector2> offset = Vector2.zero;

        private IMovementSource movementSource;
        private Rigidbody2D rb;
        private Collider2D bodyCollider;
        private Collider2D[] navigationColliders;
        private MapNavigationRuntime navigation;
        private NavigationPlanningOperation planningOperation;
        private BallisticJumpExecutor executor;
        private JumpNavigationParameters jumpParameters;
        private Vector2 bodySize;

        /// <summary>Captures the target and prepares exactly one jump action.</summary>
        public override void Start()
        {
            ClearExecution();

            if (!TryResolveDependencies()) return;
            if (!TryReadParameters(out jumpParameters, out Vector2 targetOffset)) return;
            if (!Enum.IsDefined(typeof(JumpTargetMode), targetMode)
                || !Enum.IsDefined(typeof(TargetMeasurement), targetMeasurement)
                || !Enum.IsDefined(typeof(MovementGoal), goal)
                || !Enum.IsDefined(typeof(DistanceMetric), distanceMetric))
            {
                End(false);
                return;
            }
            if (!TryResolveTarget(targetOffset, out Bounds targetBounds, out Vector2 directLanding)) return;
            if (!TryGetReadyNavigation(out INavigationWorld world)) return;

            Vector2 start = NavigationBodyGeometry.GetGroundAnchor(navigationColliders);
            if (!NavigationNumeric.IsFinite(start))
            {
                End(false);
                return;
            }

            if (targetMode == JumpTargetMode.Direct)
            {
                TryStartDirectJump(world, start, directLanding);
                return;
            }

            float arrivalTolerance = reachDistance == null || !reachDistance.HasValue
                ? 0f
                : reachDistance.NumericValue;
            if (!NavigationNumeric.IsFinite(arrivalTolerance) || arrivalTolerance < 0f)
            {
                End(false);
                return;
            }

            bool requiresLineOfSight = goal == MovementGoal.Confront
                || goal == MovementGoal.FiringPosition;
            NavigationGoalRequest request = goal == MovementGoal.Confront
                ? NavigationGoalRequest.GroundRange(targetBounds, arrivalTolerance, true)
                : NavigationGoalRequest.Proximity(targetBounds, distanceMetric, arrivalTolerance,
                    requiresLineOfSight);
            planningOperation = navigation.PlanJumpAsync(start, request, jumpParameters, CancellationToken);
        }

        /// <summary>Advances planning or the committed single jump on the fixed-step path.</summary>
        public override void FixedUpdate()
        {
            if (IsComplete) return;
            if (navigation == null || navigation.IsDisposed)
            {
                Finish(false);
                return;
            }
            if (movementSource == null)
            {
                End(false);
                return;
            }

            if (!movementSource.CanMove)
            {
                executor?.ResetProgressBaseline();
                return;
            }

            if (planningOperation != null)
            {
                if (!planningOperation.IsCompleted) return;
                NavigationPlanningOperation completed = planningOperation;
                planningOperation = null;
                if (completed.Exception != null)
                {
                    FinishException(completed.Exception);
                    return;
                }

                NavigationRoute route = completed.Result;
                if (route == null)
                {
                    Finish(false);
                    return;
                }

                if (route.Count == 0)
                {
                    Finish(true);
                    return;
                }

                if (!TryStartPlannedJump(route))
                {
                    Finish(false);
                    return;
                }
            }

            if (executor == null)
            {
                Finish(false);
                return;
            }

            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            if (result.Status == ExecutionStatus.Completed) Finish(true);
            else if (result.Status == ExecutionStatus.Failed) Finish(false);
        }

        /// <summary>Releases pending planning, the traversal executor, and its collision lease.</summary>
        public override void OnDestroy() => ClearExecution();

        private bool TryResolveDependencies()
        {
            movementSource = Script as IMovementSource;
            if (movementSource == null)
            {
                Exception(new InvalidOperationException(
                    $"{nameof(FixedJump)} requires its control target to implement {nameof(IMovementSource)}."));
                return false;
            }

            if (!gameObject.TryGetComponent(out rb)
                || !gameObject.TryGetComponent(out bodyCollider))
            {
                Exception(new InvalidOperationException(
                    $"{nameof(FixedJump)} requires Rigidbody2D and Collider2D on its AI GameObject."));
                return false;
            }

            navigationColliders = NavigationBodyGeometry.GetColliders(rb);
            if (navigationColliders == null || navigationColliders.Length == 0)
            {
                Exception(new InvalidOperationException(
                    $"{nameof(FixedJump)} requires at least one enabled navigation collider."));
                return false;
            }

            bodySize = NavigationBodyGeometry.GetWorldAabbSize(navigationColliders);
            if (!NavigationNumeric.IsFinite(bodySize) || bodySize.x <= 0f || bodySize.y <= 0f)
            {
                End(false);
                return false;
            }

            navigation = NavigationRuntimeContext.Current;
            if (navigation == null || navigation.IsDisposed)
            {
                End(false);
                return false;
            }

            return true;
        }

        private bool TryReadParameters(out JumpNavigationParameters parameters, out Vector2 targetOffset)
        {
            parameters = default;
            targetOffset = Vector2.zero;
            if (jumpHeight == null || jumpLength == null)
            {
                End(false);
                return false;
            }

            float height = jumpHeight.NumericValue;
            float length = jumpLength.NumericValue;
            targetOffset = offset == null || !offset.HasValue ? Vector2.zero : offset.Vector2Value;
            if (!NavigationNumeric.IsFinite(height) || height <= 0f
                || !NavigationNumeric.IsFinite(length) || length < 0f
                || !NavigationNumeric.IsFinite(targetOffset))
            {
                End(false);
                return false;
            }

            parameters = new JumpNavigationParameters(
                bodySize,
                Physics2D.gravity,
                rb.gravityScale,
                rb.linearDamping,
                height,
                length,
                Time.fixedDeltaTime);
            return true;
        }

        private bool TryResolveTarget(Vector2 targetOffset, out Bounds targetBounds, out Vector2 directLanding)
        {
            targetBounds = default;
            directLanding = default;
            if (target == null || target.IsNull)
            {
                End(false);
                return false;
            }

            if (target.IsVector)
            {
                Vector2 point = target.Vector2Value + targetOffset;
                if (!NavigationNumeric.IsFinite(point))
                {
                    End(false);
                    return false;
                }

                targetBounds = new Bounds(point, Vector3.zero);
                directLanding = point;
                return true;
            }

            if (!target.IsFromGameObject)
            {
                End(false);
                return false;
            }

            if (targetMode == JumpTargetMode.PlannedStep
                && targetMeasurement == TargetMeasurement.TransformPosition)
            {
                Vector2 point = (Vector2)target.PositionValue + targetOffset;
                if (!NavigationNumeric.IsFinite(point))
                {
                    End(false);
                    return false;
                }

                targetBounds = new Bounds(point, Vector3.zero);
                directLanding = point;
                return true;
            }

            Collider2D[] targetColliders = NavigationBodyGeometry.GetTargetColliders(target.GameObjectValue);
            if (targetColliders == null || targetColliders.Length == 0)
            {
                End(false);
                return false;
            }

            targetBounds = NavigationBodyGeometry.GetMergedBounds(targetColliders);
            targetBounds.center += (Vector3)targetOffset;
            directLanding = new Vector2(targetBounds.center.x, targetBounds.min.y);
            if (!NavigationNumeric.IsFinite(directLanding) || !NavigationNumeric.IsFinite(targetBounds))
            {
                End(false);
                return false;
            }

            return true;
        }

        private bool TryGetReadyNavigation(out INavigationWorld world)
        {
            world = null;
            if (navigation == null || !navigation.IsReady || !navigation.TryGetWorld(out world))
            {
                End(false);
                return false;
            }

            return world != null;
        }

        private void TryStartDirectJump(INavigationWorld world, Vector2 start, Vector2 landing)
        {
            GroundJumpParameters parameters = CreateGeometryParameters(jumpParameters);
            if (!navigation.TryGetJumpSolver(out GroundJumpSolver jumpSolver)
                || !jumpSolver.TrySolve(start, landing, parameters, out JumpTrajectorySolution solved))
            {
                Finish(false);
                return;
            }

            JumpRouteSegment segment = GroundJumpGeometry.CreateSegment(world, solved, bodySize, parameters.SupportSnapDistance);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(bodyCollider, segment, navigation, out OneWayPlatformCollisionLease lease))
            {
                Finish(false);
                return;
            }

            BeginExecutor(solved, lease);
        }

        private bool TryStartPlannedJump(NavigationRoute route)
        {
            if (route.Segments[0] is not JumpRouteSegment jump
                || !navigation.TryGetWorld(out INavigationWorld world))
                return false;

            Vector2 start = NavigationBodyGeometry.GetGroundAnchor(navigationColliders);
            if (!navigation.TryResolvePlanningGroundSupport(
                start, bodySize, out _, out NavigationSupport currentSupport)
                || !navigation.TryResolvePlanningGroundSupport(
                    jump.LaunchSupport, bodySize, out _, out NavigationSupport plannedSupport)
                || currentSupport.Surface != plannedSupport.Surface)
                return false;

            GroundJumpParameters parameters = CreateGeometryParameters(jumpParameters);
            if (!navigation.TryGetJumpSolver(out GroundJumpSolver jumpSolver)
                || !jumpSolver.TrySolve(start, jump.PlannedLanding, parameters,
                    out JumpTrajectorySolution solved))
                return false;

            JumpRouteSegment segment = GroundJumpGeometry.CreateSegment(world, solved, bodySize, parameters.SupportSnapDistance);
            if (!OneWayPlatformCollisionLease.TryCreateForSegment(bodyCollider, segment, navigation, out OneWayPlatformCollisionLease lease))
                return false;

            BeginExecutor(solved, lease);
            return true;
        }

        private GroundJumpParameters CreateGeometryParameters(JumpNavigationParameters source)
            => new(
                source.BodySize,
                source.Gravity,
                source.GravityScale,
                source.LinearDamping,
                source.JumpHeight,
                source.JumpLength,
                source.SimulationTimeStep,
                NavigationWorldQueries.SupportSnapDistance,
                GroundTraversalEndpointPolicy.VerticalSupportTolerance);

        private void BeginExecutor(JumpTrajectorySolution solved, OneWayPlatformCollisionLease lease)
        {
            executor = new BallisticJumpExecutor(rb, bodyCollider, navigationColliders, navigation.CreateTerrainFilter(), solved, lease);
            InvokeJumpCallback();
        }

        private void Finish(bool result)
        {
            ClearExecution();
            End(result);
        }

        private void FinishException(Exception exception)
        {
            ClearExecution();
            Exception(exception);
        }

        private void ClearExecution()
        {
            executor?.Dispose();
            executor = null;
            planningOperation = null;
            navigation = null;
            navigationColliders = null;
            bodyCollider = null;
            rb = null;
            movementSource = null;
        }

        /// <summary>Invokes the standard jump callback after a trajectory executor is ready.</summary>
        private void InvokeJumpCallback()
        {
            foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
            {
                MonoBehaviour callbackTarget = component;
                try { CallbackTable.Call(ref callbackTarget, JumpCallbackMethodName); }
                catch { }
            }
        }


    }
}
