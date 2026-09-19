using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Executes one ground traversal step through an explicitly supplied 2D physics body.
    /// </summary>
    public sealed class GroundTraversalExecutor : MovementExecutor
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker MovementMarker = new("Aethiumian.AI/MovementExecutor");
#endif

        private const int HitCapacity = 8;

        // Tick records action-specific progress; only the base consumes it and advances timeout.
        private ProgressObservation progress;

        private readonly Rigidbody2D body;
        private readonly Collider2D bodyCollider;
        private readonly IReadOnlyList<Collider2D> navigationColliders;
        private readonly float speed;
        private readonly float accelerationRate;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[HitCapacity];
        private readonly ContactFilter2D terrainFilter;

        /// <summary>Identifies the physical action currently owned by the executor.</summary>
        public enum ActionKind
        {
            None,
            GroundMove,
            Jump,
            Fall,
            DropThrough,
        }

        private ActionKind currentAction;
        private Vector2 actionStart;
        private Vector2 actionEnd;
        private Vector2 ledgeExit;
        private JumpTrajectorySolution jumpTrajectory;
        private OneWayPlatformCollisionLease oneWayPlatformLease;
        private float elapsedSeconds;
        private bool jumpLaunched;
        private Vector2 previousJumpAnchor;
        private bool hasPreviousJumpAnchor;
        private bool fallReleased;
        private bool awaitingEndpointContactResolution;
        private float bestProgress;
        private bool hasProgressBaseline;


        /// <summary>
        /// Gets the current action without advancing its execution.
        /// </summary>
        public ActionKind CurrentAction => currentAction;
        /// <summary>
        /// Gets the current action's planned start point.
        /// </summary>
        public Vector2 CurrentActionStart => actionStart;
        /// <summary>
        /// Gets the current action's planned end point.
        /// </summary>
        public Vector2 CurrentActionEnd => actionEnd;
        /// <summary>
        /// Gets the current fall action's planned ledge exit.
        /// </summary>
        public Vector2 CurrentLedgeExit => ledgeExit;
        /// <summary>
        /// Gets the current jump solution, if the action is a jump.
        /// </summary>
        public JumpTrajectorySolution CurrentJumpTrajectory => jumpTrajectory;
        /// <summary>
        /// Gets elapsed time in the current action.
        /// </summary>
        public float CurrentActionElapsedSeconds => elapsedSeconds;
        /// <summary>
        /// Gets the number of collision pairs owned by the current action.
        /// </summary>
        public int PlatformCollisionLeaseCount => oneWayPlatformLease?.EntryCount ?? 0;
        /// <summary>Gets whether the current jump has applied its launch impulse.</summary>
        internal bool HasJumpLaunched => jumpLaunched;

        /// <summary>
        /// Captures borrowed physics inputs and the per-execution stall timeout (zero disables it).
        /// Construction and Begin methods do not move the body; ordinary physics writes occur in Tick.
        /// </summary>
        public GroundTraversalExecutor(
            Rigidbody2D body,
            Collider2D bodyCollider,
            ContactFilter2D terrainFilter,
            float speed,
            float accelerationRate,
            IReadOnlyList<Collider2D> navigationColliders = null,
            float maximumIdleDuration = 0f) : base(maximumIdleDuration)
        {
            this.body = body ? body : throw new ArgumentNullException(nameof(body));
            this.bodyCollider = bodyCollider ? bodyCollider : throw new ArgumentNullException(nameof(bodyCollider));
            Validate.NonNegativeFinite(speed, nameof(speed));
            Validate.NonNegativeFinite(accelerationRate, nameof(accelerationRate));
            this.speed = speed;
            this.accelerationRate = accelerationRate;
            this.navigationColliders = navigationColliders ?? new[] { bodyCollider };
            this.terrainFilter = terrainFilter;
        }

        /// <summary>Reports this step's physical progress without exposing watchdog policy to nodes.</summary>
        protected override ProgressObservation ObserveProgress() => progress;

        /// <summary>
        /// Sets a ground segment without writing physics. Same-direction, same-level updates
        /// preserve progress and idle timing; other actions start a new execution lifetime.
        /// </summary>
        public void SetGroundMove(Vector2 start, Vector2 end)
        {
            ThrowIfDisposed();
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            if (IsExecuting && currentAction == ActionKind.GroundMove
                && (actionEnd.x - actionStart.x) * (end.x - start.x) > 0f
                && Mathf.Abs(start.y - actionStart.y) <= VerticalSupportTolerance
                && Mathf.Abs(end.y - actionEnd.y) <= VerticalSupportTolerance)
            {
                actionEnd = end;
                return;
            }
            BeginAction(ActionKind.GroundMove, start, end);
        }

        /// <summary>Begins one ballistic jump action from its freshly solved trajectory.</summary>
        public void BeginJump(JumpTrajectorySolution trajectory) => BeginJump(trajectory, null);

        /// <summary>Begins a ballistic jump with a planner-resolved, not-yet-enabled collision lease.</summary>
        public void BeginJump(JumpTrajectorySolution trajectory, OneWayPlatformCollisionLease lease)
        {
            ThrowIfDisposed();
            if (trajectory == null) throw new ArgumentNullException(nameof(trajectory));
            BeginAction(ActionKind.Jump, trajectory.StartPosition, trajectory.LandingPosition);
            jumpTrajectory = trajectory;
            previousJumpAnchor = GetGroundAnchor();
            hasPreviousJumpAnchor = true;
            oneWayPlatformLease = lease;
        }

        /// <summary>Begins one ledge-exit and fall action without writing physics state.</summary>
        public void BeginFall(Vector2 start, Vector2 ledgeExit, Vector2 end)
        {
            ThrowIfDisposed();
            Validate.Finite(start, nameof(start));
            Validate.Finite(ledgeExit, nameof(ledgeExit));
            Validate.Finite(end, nameof(end));
            if (!Mathf.Approximately(ledgeExit.y, start.y) || end.y >= ledgeExit.y)
                throw new ArgumentException("Fall ledge exit must be horizontally aligned with the start and above the end.", nameof(ledgeExit));
            BeginAction(ActionKind.Fall, start, end);
            this.ledgeExit = ledgeExit;
        }

        /// <summary>Begins one one-way drop-through action without writing physics state.</summary>
        public void BeginDropThrough(Vector2 start, Vector2 end)
        {
            ThrowIfDisposed();
            BeginAction(ActionKind.DropThrough, start, end);
            oneWayPlatformLease = OneWayPlatformCollisionLease.CreateForDropThrough(bodyCollider);
            previousJumpAnchor = GetGroundAnchor();
            hasPreviousJumpAnchor = true;
        }

        private void BeginAction(ActionKind action, Vector2 start, Vector2 end)
        {
            ThrowIfDisposed();
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            BeginExecution();
            actionStart = start;
            actionEnd = end;
            currentAction = action;
            elapsedSeconds = 0f;
            jumpLaunched = false;
            fallReleased = false;
            awaitingEndpointContactResolution = false;
            bestProgress = 0f;
            hasProgressBaseline = false;
            progress = ProgressObservation.NotMonitored;
        }


        /// <summary>Executes the active physical phase and records its progress for the base watchdog.</summary>
        protected override ExecutionResult Tick_Internal(float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var marker = MovementMarker.Auto();
#endif
            elapsedSeconds += deltaTime;
            progress = ProgressObservation.Waiting;

            ExecutionResult result = currentAction switch
            {
                ActionKind.GroundMove => TickGroundMove(deltaTime),
                ActionKind.Jump => TickJump(deltaTime),
                ActionKind.Fall => TickFall(deltaTime),
                ActionKind.DropThrough => TickDropThrough(deltaTime),
                _ => throw new InvalidOperationException("The active action is unsupported."),
            };

            return result;
        }

        /// <summary>Reinitializes the current phase progress baseline after an explicit pause.</summary>
        protected override void ResetProgressBaselineCore()
        {
            if (currentAction == ActionKind.None) return;
            hasProgressBaseline = false;
            if (currentAction is ActionKind.Jump or ActionKind.Fall or ActionKind.DropThrough)
            {
                previousJumpAnchor = GetGroundAnchor();
                hasPreviousJumpAnchor = true;
            }
        }


        private ExecutionResult TickGroundMove(float deltaTime)
        {
            float currentX = GetGroundAnchor().x;
            float displacement = actionEnd.x - currentX;
            progress = RecordProgress(Mathf.Sign(actionEnd.x - actionStart.x) * currentX) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
            float plannedDisplacement = actionEnd.x - actionStart.x;
            // Arrival is directional and one-sided: a ground move travels along x from actionStart to
            // actionEnd, so it may only report complete once the body has reached actionEnd travelling
            // that way. Overshooting is deliberately unbounded - what happens after arrival belongs to
            // the caller and the planner, not to this predicate. A symmetric band would let the move
            // report complete while the body is still short of its endpoint, which is how an action
            // ends with its goal unmet.
            if (plannedDisplacement == 0f
                || Mathf.Sign(plannedDisplacement) * (currentX - actionEnd.x) >= 0f)
            {
                return ExecutionResult.Completed;
            }

            if (!IsGrounded() || HasObstacle(Mathf.Sign(displacement), speed * deltaTime + NavigationConstant.GroundProbeDistance))
                return ExecutionResult.Failure(ExecutionFailureReason.Obstructed);

            // A remaining distance the body can cover in one step is landed exactly instead of being
            // driven by velocity: writing a proportional velocity decelerates asymptotically and can
            // never arrive, raising the speed instead overshoots and can carry the body off its
            // platform, and falling slower than the executor motion floor stalls the move in place. Landing exactly
            // keeps arrival one-sided with no residual and no overshoot.
            if (Mathf.Abs(displacement) <= speed * deltaTime)
            {
                body.position = new Vector2(actionEnd.x, body.position.y);
                return ExecutionResult.Completed;
            }

            float expectedVelocity = Mathf.Sign(displacement) * speed;
            float horizontalVelocity = Mathf.Lerp(body.linearVelocityX, expectedVelocity, accelerationRate);
            if (Mathf.Abs(horizontalVelocity) <= NavigationConstant.MinimumMotion) return ExecutionResult.Running;

            body.linearVelocity = new Vector2(horizontalVelocity, body.linearVelocityY);
            return ExecutionResult.Running;
        }

        private ExecutionResult TickJump(float deltaTime)
        {
            if (!jumpLaunched)
            {
                if (!IsGrounded()) return ExecutionResult.Running;

                Vector2 impulse = body.mass * (jumpTrajectory.InitialVelocity - body.linearVelocity);
                body.AddForce(impulse, ForceMode2D.Impulse);
                oneWayPlatformLease?.Enable();
                jumpLaunched = true;
                elapsedSeconds = 0f;
                ResetProgressBaseline();
                return ExecutionResult.Running;
            }

            // The solved horizontal displacement is exact at FlightDuration. Stop the
            // residual velocity while waiting for the contact sample so one extra
            // physics step cannot carry the body past the planned landing.
            if (elapsedSeconds >= jumpTrajectory.FlightDuration && !IsGrounded())
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocityY);
            }

            oneWayPlatformLease?.Tick();
            Vector2 currentAnchor = GetGroundAnchor();
            if (elapsedSeconds < jumpTrajectory.FlightDuration)
            {
                // Horizontal distance alone cannot describe progress during a solved flight.
                // Landing progress monitoring starts only after the scheduled flight completes.
                progress = ProgressObservation.NotMonitored;
                previousJumpAnchor = currentAnchor;
                return ExecutionResult.Running;
            }

            bool hasSupport = TryGetGroundSupport(out RaycastHit2D support);
            if (!hasSupport)
            {
                progress = RecordProgress(-Mathf.Abs(actionEnd.x - currentAnchor.x)) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
                ExecutionResult crossingResult = ConsumeEndpointCrossing(currentAnchor);
                previousJumpAnchor = currentAnchor;
                return crossingResult;
            }

            if (body.linearVelocityY > 0f)
                return ExecutionResult.Running;

            if (Mathf.Abs(support.point.y - actionEnd.y) > VerticalSupportTolerance)
            {
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (awaitingEndpointContactResolution)
            {
                awaitingEndpointContactResolution = false;
                bool stable = IsLandingAtEndpoint(currentAnchor, deltaTime);
                previousJumpAnchor = currentAnchor;
                return stable ? ExecutionResult.Completed : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            bool reachedLanding = Mathf.Abs(currentAnchor.x - actionEnd.x) <= GetJumpHorizontalCompletionTolerance(deltaTime)
                || hasPreviousJumpAnchor && CrossedLanding(previousJumpAnchor, currentAnchor, actionEnd)
                || IsWithinLandingDrift(currentAnchor, actionEnd, deltaTime);
            progress = RecordProgress(-Mathf.Abs(actionEnd.x - currentAnchor.x)) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
            previousJumpAnchor = currentAnchor;
            return reachedLanding ? ExecutionResult.Completed : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
        }

        private ExecutionResult TickFall(float deltaTime)
        {
            if (!fallReleased)
            {
                Vector2 currentAnchor = GetGroundAnchor();
                float displacement = ledgeExit.x - currentAnchor.x;
                progress = RecordProgress(-Mathf.Abs(displacement)) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
                float ledgeExitTolerance = GroundTraversalEndpointPolicy.GetHorizontalTransitionTolerance(speed, deltaTime);
                if (Mathf.Abs(displacement) > ledgeExitTolerance)
                {
                    if (HasObstacle(Mathf.Sign(displacement), speed * deltaTime + NavigationConstant.GroundProbeDistance))
                        return ExecutionResult.Failure(ExecutionFailureReason.Obstructed);
                    float expectedSpeed = Mathf.Min(speed, Mathf.Abs(displacement) / deltaTime);
                    float expectedVelocity = Mathf.Sign(displacement) * expectedSpeed;
                    float horizontalVelocity = Mathf.Lerp(body.linearVelocityX, expectedVelocity, accelerationRate);
                    if (Mathf.Abs(horizontalVelocity) > NavigationConstant.MinimumMotion)
                    {
                        body.linearVelocity = new Vector2(horizontalVelocity, body.linearVelocityY);
                    }
                    return ExecutionResult.Running;
                }

                fallReleased = true;
                ResetProgressBaseline();
                previousJumpAnchor = currentAnchor;
                hasPreviousJumpAnchor = true;
                return ExecutionResult.Running;
            }

            Vector2 anchor = GetGroundAnchor();
            progress = RecordProgress(-anchor.y) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
            if (!TryGetGroundSupport(out RaycastHit2D support))
            {
                ExecutionResult crossingResult = ConsumeEndpointCrossing(anchor);
                previousJumpAnchor = anchor;
                return crossingResult;
            }

            if (body.linearVelocityY > 0f)
                return ExecutionResult.Running;

            if (Mathf.Abs(support.point.y - actionEnd.y) > VerticalSupportTolerance)
            {
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (awaitingEndpointContactResolution)
            {
                awaitingEndpointContactResolution = false;
                bool stable = IsLandingAtEndpoint(anchor, deltaTime);
                previousJumpAnchor = anchor;
                return stable ? ExecutionResult.Completed : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            previousJumpAnchor = anchor;
            return IsLandingAtEndpoint(anchor, deltaTime)
                ? ExecutionResult.Completed
                : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
        }

        private ExecutionResult TickDropThrough(float deltaTime)
        {
            oneWayPlatformLease ??= OneWayPlatformCollisionLease.CreateForDropThrough(bodyCollider);
            oneWayPlatformLease.Tick();

            Vector2 anchor = GetGroundAnchor();
            progress = RecordProgress(-anchor.y) ? ProgressObservation.Advanced : ProgressObservation.Waiting;
            if (!TryGetGroundSupport(out RaycastHit2D support))
            {
                if (anchor.y > actionEnd.y + VerticalSupportTolerance)
                {
                    ExecutionResult crossingResult = ConsumeEndpointCrossing(anchor);
                    previousJumpAnchor = anchor;
                    return crossingResult;
                }

                return ExecutionResult.Failure(ExecutionFailureReason.InvalidExecution);
            }

            if (awaitingEndpointContactResolution)
            {
                awaitingEndpointContactResolution = false;
                previousJumpAnchor = anchor;
                if (Mathf.Abs(support.point.y - actionEnd.y) > VerticalSupportTolerance)
                {
                    return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
                }

                return IsLandingAtEndpoint(anchor, deltaTime)
                    ? ExecutionResult.Completed
                    : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (Mathf.Abs(support.point.y - actionEnd.y) <= VerticalSupportTolerance)
            {
                previousJumpAnchor = anchor;
                return IsLandingAtEndpoint(anchor, deltaTime)
                    ? ExecutionResult.Completed
                    : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (support.point.y < actionEnd.y - VerticalSupportTolerance)
            {
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (!oneWayPlatformLease.TryAddDescendingSupport(support.collider, support.point.y))
            {
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            oneWayPlatformLease.Enable();
            body.linearVelocity = new Vector2(body.linearVelocityX, -speed);
            previousJumpAnchor = anchor;
            return ExecutionResult.Running;
        }

        private ExecutionResult ConsumeEndpointCrossing(Vector2 currentAnchor)
        {
            if (awaitingEndpointContactResolution)
            {
                awaitingEndpointContactResolution = false;
                return ExecutionResult.Failure(ExecutionFailureReason.InvalidExecution);
            }

            if (hasPreviousJumpAnchor
                && previousJumpAnchor.y > actionEnd.y + VerticalSupportTolerance
                && currentAnchor.y <= actionEnd.y + VerticalSupportTolerance)
            {
                awaitingEndpointContactResolution = true;
                // Give the next physics contact sample its existing resolution window;
                // it must not be mistaken for idle time at the crossing boundary.
                progress = ProgressObservation.NotMonitored;
                return ExecutionResult.Running;
            }

            return ExecutionResult.Running;
        }

        private bool IsLandingAtEndpoint(Vector2 anchor, float deltaTime)
            => Mathf.Abs(anchor.x - actionEnd.x)
                <= GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(speed, deltaTime);

        private bool RecordProgress(float value)
        {
            if (!hasProgressBaseline)
            {
                bestProgress = value;
                hasProgressBaseline = true;
                return false;
            }
            if (value <= bestProgress + NavigationWorldQueries.GeometryEpsilon) return false;
            bestProgress = value;
            return true;
        }

        private void ClearAction()
        {
            currentAction = ActionKind.None;
            actionStart = default;
            actionEnd = default;
            ledgeExit = default;
            jumpTrajectory = null;
            elapsedSeconds = 0f;
            jumpLaunched = false;
            fallReleased = false;
            previousJumpAnchor = default;
            hasPreviousJumpAnchor = false;
            awaitingEndpointContactResolution = false;
            bestProgress = 0f;
            hasProgressBaseline = false;
            progress = ProgressObservation.NotMonitored;
            oneWayPlatformLease = null;
        }

        /// <summary>Restores collision pairs before dropping action data; borrowed body velocity is unchanged.</summary>
        protected override void ReleaseExecutionResources()
        {
            oneWayPlatformLease?.Restore();
            ClearAction();
        }

        private bool IsGrounded() => TryGetGroundSupport(out _);

        private bool IsAtJumpLanding(Vector2 landing, float deltaTime)
        {
            if (!TryGetGroundSupport(out RaycastHit2D support)) return false;
            Vector2 anchor = GetGroundAnchor();
            if (Mathf.Abs(support.point.y - landing.y) > VerticalSupportTolerance) return false;
            return Mathf.Abs(anchor.x - landing.x) <= GetJumpHorizontalCompletionTolerance(deltaTime);
        }

        /// <summary>Recognizes a landing crossed between fixed physics samples without widening contact tolerance.</summary>
        private static bool CrossedLanding(Vector2 previous, Vector2 current, Vector2 landing)
        {
            float previousDelta = landing.x - previous.x;
            float currentDelta = landing.x - current.x;
            return previousDelta * currentDelta <= 0f;
        }

        /// <summary>Allows one fixed-step of post-flight drift while retaining the normal contact tolerance.</summary>
        private bool IsWithinLandingDrift(Vector2 current, Vector2 landing, float deltaTime)
            => Mathf.Abs(current.x - landing.x) <= GetJumpHorizontalCompletionTolerance(deltaTime)
                && Mathf.Abs(current.y - landing.y) <= VerticalSupportTolerance;

        /// <summary>Gets the endpoint tolerance from the jump's planned horizontal velocity.</summary>
        private float GetJumpHorizontalCompletionTolerance(float deltaTime)
            => GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(
                Mathf.Abs(jumpTrajectory.InitialVelocity.x), deltaTime);

        private bool TryGetGroundSupport(out RaycastHit2D nearest)
        {
            int count = bodyCollider.Cast(Vector2.down, terrainFilter, hits, NavigationConstant.GroundProbeDistance);
            float nearestDistance = float.PositiveInfinity;
            nearest = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                Collider2D collider = hit.collider;
                if (!collider || collider == bodyCollider || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                nearest = hit;
            }

            return nearest.collider;
        }

        private bool HasObstacle(float horizontalDirection, float distance)
        {
            int count = bodyCollider.Cast(new Vector2(horizontalDirection, 0f), terrainFilter, hits, distance);
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                Collider2D collider = hit.collider;
                if (collider && collider != bodyCollider && hit.normal.x * horizontalDirection < -NavigationConstant.ObstacleNormalThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        private Vector2 GetGroundAnchor() => NavigationBodyGeometry.GetGroundAnchor(navigationColliders);

        private static float VerticalSupportTolerance => GroundTraversalEndpointPolicy.VerticalSupportTolerance;
    }
}
