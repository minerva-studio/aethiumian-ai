using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Executes one validated jump and owns its physical landing state and collision lease.
    /// The movement node retains planning, cadence, callbacks, and recovery policy.
    /// </summary>
    public sealed class BallisticJumpExecutor : MovementExecutor
    {
        // Tick records action-specific progress; only the base consumes it and advances timeout.
        private ProgressObservation progress;
        /// <summary>Reports this step's physical progress without exposing watchdog policy to nodes.</summary>
        protected override ProgressObservation ObserveProgress() => progress;

        private readonly Collider2D bodyCollider;
        private readonly IReadOnlyList<Collider2D> navigationColliders;
        private readonly Rigidbody2D body;
        private readonly JumpTrajectorySolution trajectory;
        private readonly ContactFilter2D supportFilter;
        private float elapsed;
        private OneWayPlatformCollisionLease collisionLease;
        private bool launched;
        private Vector2 previousJumpAnchor;
        private float bestLandingRemaining;
        private bool hasLandingProgressBaseline;
        private bool awaitingEndpointContactResolution;



        /// <summary>
        /// Prepares one trajectory and owns the supplied collision lease. The optional timeout
        /// applies to monitored support/landing phases; zero preserves unmonitored FixedJump behavior.
        /// </summary>
        public BallisticJumpExecutor(Rigidbody2D body, Collider2D bodyCollider,
            IReadOnlyList<Collider2D> navigationColliders, ContactFilter2D supportFilter, JumpTrajectorySolution trajectory,
            OneWayPlatformCollisionLease collisionLease, float maximumIdleDuration = 0f) : base(maximumIdleDuration)
        {
            if (!body) throw new ArgumentNullException(nameof(body));
            this.bodyCollider = bodyCollider ? bodyCollider : throw new ArgumentNullException(nameof(bodyCollider));
            this.navigationColliders = navigationColliders ?? throw new ArgumentNullException(nameof(navigationColliders));
            if (navigationColliders.Count == 0) throw new ArgumentException("Navigation colliders cannot be empty.", nameof(navigationColliders));
            if (trajectory == null) throw new ArgumentNullException(nameof(trajectory));
            this.supportFilter = supportFilter;
            this.body = body;
            this.trajectory = trajectory;
            previousJumpAnchor = GetGroundAnchor();
            BeginExecution();
            this.collisionLease = collisionLease;
        }


        /// <summary>Launches once, advances flight, then evaluates the actual physical landing.</summary>
        protected override ExecutionResult Tick_Internal(float fixedDeltaTime)
        {
            // Normal flight is time-driven, not a distance-to-landing stall measurement.
            progress = ProgressObservation.NotMonitored;

            if (!launched)
            {
                if (!NavigationWorldQueries.TryGetGroundSupportPoint(bodyCollider, supportFilter, out Vector2 launchSupport))
                {
                    progress = ProgressObservation.Waiting;
                    return ExecutionResult.Running;
                }

                if (Mathf.Abs(launchSupport.y - trajectory.StartPosition.y) > NavigationWorldQueries.SupportSnapDistance
                    || Mathf.Abs(GetGroundAnchor().x - trajectory.StartPosition.x) > NavigationWorldQueries.SupportSnapDistance)
                {
                    return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
                }

                AdvanceBallistics(fixedDeltaTime);
                ResetLandingProgressBaseline();
            }
            else
            {
                AdvanceBallistics(fixedDeltaTime);
            }

            bool completedFlight = elapsed >= trajectory.FlightDuration;
            Vector2 currentAnchor = GetGroundAnchor();
            if (!completedFlight)
            {
                previousJumpAnchor = currentAnchor;
                return ExecutionResult.Running;
            }

            if (!NavigationWorldQueries.TryGetGroundSupportPoint(bodyCollider, supportFilter, out Vector2 supportPoint))
            {
                bool madeNewBestProgress = RecordLandingProgress(Mathf.Abs(currentAnchor.x - trajectory.LandingPosition.x));
                progress = madeNewBestProgress ? ProgressObservation.Advanced : ProgressObservation.Waiting;
                if (awaitingEndpointContactResolution)
                {
                    awaitingEndpointContactResolution = false;
                    return ExecutionResult.Failure(ExecutionFailureReason.InvalidExecution);
                }

                bool crossedEndpoint = CrossedEndpoint(previousJumpAnchor, currentAnchor, trajectory.LandingPosition.y);
                if (crossedEndpoint) awaitingEndpointContactResolution = true;
                previousJumpAnchor = currentAnchor;
                if (crossedEndpoint) progress = ProgressObservation.NotMonitored;
                return ExecutionResult.Running;
            }

            if (body.linearVelocityY > 0f)
                return ExecutionResult.Running;

            if (Mathf.Abs(supportPoint.y - trajectory.LandingPosition.y) > GroundTraversalEndpointPolicy.VerticalSupportTolerance)
            {
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (awaitingEndpointContactResolution)
            {
                awaitingEndpointContactResolution = false;
                return IsLandingAtEndpoint(currentAnchor, fixedDeltaTime)
                    ? ExecutionResult.Completed
                    : ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            if (Mathf.Abs(currentAnchor.x - trajectory.LandingPosition.x) > GetHorizontalLandingTolerance(fixedDeltaTime))
            {
                // Flight is over and support is established; no steering remains to repair the landing.
                return ExecutionResult.Failure(ExecutionFailureReason.UnexpectedSupport);
            }

            return ExecutionResult.Completed;
        }

        /// <summary>Reinitializes progress measurement after an intentional movement pause.</summary>
        protected override void ResetProgressBaselineCore()
        {
            ThrowIfDisposed();
            previousJumpAnchor = GetGroundAnchor();
            hasLandingProgressBaseline = false;
        }

        /// <summary>Restores owned collision pairs on every terminal outcome, including base-detected stalls.</summary>
        protected override void ReleaseExecutionResources()
        {
            collisionLease?.Restore();
            collisionLease = null;
        }

        private Vector2 GetGroundAnchor() => NavigationBodyGeometry.GetGroundAnchor(navigationColliders);

        /// <summary>Advances flight time, applies the initial impulse, and services the collision lease.</summary>
        private bool AdvanceBallistics(float fixedDeltaTime)
        {
            if (elapsed >= trajectory.FlightDuration) return true;
            if (!launched)
            {
                body.AddForce(body.mass * (trajectory.InitialVelocity - body.linearVelocity), ForceMode2D.Impulse);
                launched = true;
                collisionLease?.Enable();
            }
            else collisionLease?.Tick();
            elapsed = Mathf.Min(trajectory.FlightDuration, elapsed + fixedDeltaTime);
            return elapsed >= trajectory.FlightDuration;
        }

        private void ResetLandingProgressBaseline()
        {
            bestLandingRemaining = Mathf.Abs(GetGroundAnchor().x - trajectory.LandingPosition.x);
            hasLandingProgressBaseline = true;
            previousJumpAnchor = GetGroundAnchor();
        }

        private bool RecordLandingProgress(float remainingDistance)
        {
            if (!hasLandingProgressBaseline)
            {
                bestLandingRemaining = remainingDistance;
                hasLandingProgressBaseline = true;
                return false;
            }

            if (remainingDistance >= bestLandingRemaining - NavigationWorldQueries.GeometryEpsilon) return false;
            bestLandingRemaining = remainingDistance;
            return true;
        }

        private bool CrossedEndpoint(Vector2 previous, Vector2 current, float endpointY)
            => previous.y > endpointY + GroundTraversalEndpointPolicy.VerticalSupportTolerance
                && current.y <= endpointY + GroundTraversalEndpointPolicy.VerticalSupportTolerance;

        private bool IsLandingAtEndpoint(Vector2 anchor, float fixedDeltaTime)
            => Mathf.Abs(anchor.x - trajectory.LandingPosition.x) <= GetHorizontalLandingTolerance(fixedDeltaTime);

        private float GetHorizontalLandingTolerance(float fixedDeltaTime)
            => GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(Mathf.Abs(trajectory.InitialVelocity.x), fixedDeltaTime);

    }
}
