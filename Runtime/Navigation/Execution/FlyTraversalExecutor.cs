using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Executes one aerial traversal step through fixed-step velocity changes.</summary>
    public sealed class FlyTraversalExecutor : MovementExecutor
    {
        private const int MaximumCastHits = 8;

        private readonly Rigidbody2D body;
        private readonly Collider2D bodyCollider;
        private readonly IReadOnlyList<Collider2D> navigationColliders;
        private readonly float speed;
        private readonly float flexibility;
        private readonly ContactFilter2D collisionFilter;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[MaximumCastHits];
        private Vector2 steeringTarget;
        private Vector2 waypointStart;
        private float completionDistance;
        private float bestRemainingDistance;

        /// <summary>Creates an aerial executor with explicit physics and locomotion inputs.</summary>
        /// <param name="maximumIdleDuration">Fixed-waypoint no-progress timeout; zero disables monitoring.</param>
        public FlyTraversalExecutor(
            Rigidbody2D body,
            Collider2D bodyCollider,
            float speed,
            float flexibility,
            ContactFilter2D collisionFilter,
            IReadOnlyList<Collider2D> navigationColliders = null,
            float maximumIdleDuration = 0f) : base(maximumIdleDuration)
        {
            if (!body) throw new ArgumentNullException(nameof(body));
            if (!bodyCollider) throw new ArgumentNullException(nameof(bodyCollider));
            Validate.NonNegativeFinite(speed, nameof(speed));
            ValidateRatio(flexibility, nameof(flexibility));

            this.body = body;
            this.bodyCollider = bodyCollider;
            this.speed = speed;
            this.flexibility = flexibility;
            this.collisionFilter = collisionFilter;
            this.navigationColliders = navigationColliders ?? new[] { bodyCollider };
        }

        /// <summary>
        /// Sets the current waypoint without writing velocity. Updating an executing waypoint
        /// preserves idle time and adjusts only the distance baseline for the changed target.
        /// </summary>
        public void SetWaypoint(Vector2 start, Vector2 end, float completionDistance)
        {
            ThrowIfDisposed();
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            Validate.NonNegativeFinite(completionDistance, nameof(completionDistance));
            Vector2 bodyCenter = NavigationBodyGeometry.GetBodyCenter(navigationColliders);
            if (IsExecuting)
                bestRemainingDistance += Vector2.Distance(end, bodyCenter) - Vector2.Distance(steeringTarget, bodyCenter);
            else
            {
                BeginExecution();
                bestRemainingDistance = Vector2.Distance(end, bodyCenter);
            }
            waypointStart = start;
            steeringTarget = end;
            this.completionDistance = completionDistance;
        }

        /// <summary>Steers with the current smoothing history; collision sliding remains a valid running step.</summary>
        protected override ExecutionResult Tick_Internal(float fixedDeltaTime)
        {
            Vector2 displacement = steeringTarget - NavigationBodyGeometry.GetBodyCenter(navigationColliders);
            float remainingDistance = displacement.magnitude;
            if (remainingDistance <= completionDistance)
            {
                return ExecutionResult.Completed;
            }

            float stoppingSpeed = remainingDistance / fixedDeltaTime;
            Vector2 desiredDirection = displacement / remainingDistance;

            Vector2 targetVelocity = desiredDirection * Mathf.Min(speed, stoppingSpeed);
            Vector2 nextVelocity = Vector2.Lerp(body.linearVelocity, targetVelocity, flexibility);
            body.linearVelocity = ClampToCollision(nextVelocity, fixedDeltaTime, out bool blocked);
            // No legal velocity is a failure of this action, not a completed waypoint.
            // The node chooses whether to replan or finish; no retry counter lives here.
            if (blocked) return ExecutionResult.Failure(ExecutionFailureReason.Obstructed);
            Vector2 segment = steeringTarget - waypointStart;
            Vector2 bodyCenter = NavigationBodyGeometry.GetBodyCenter(navigationColliders);
            if (segment.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                || Vector2.Dot(bodyCenter - waypointStart, segment) >= segment.sqrMagnitude)
                return ExecutionResult.Completed;
            return ExecutionResult.Running;
        }


        protected override ProgressObservation ObserveProgress()
        {
            float remaining = Vector2.Distance(steeringTarget,
                NavigationBodyGeometry.GetBodyCenter(navigationColliders));
            if (remaining < bestRemainingDistance - NavigationWorldQueries.GeometryEpsilon)
            {
                bestRemainingDistance = remaining;
                return ProgressObservation.Advanced;
            }
            return ProgressObservation.Waiting;
        }

        protected override void ResetProgressBaselineCore()
        {
            bestRemainingDistance = Vector2.Distance(steeringTarget,
                NavigationBodyGeometry.GetBodyCenter(navigationColliders));
        }

        private Vector2 ClampToCollision(Vector2 velocity, float fixedDeltaTime, out bool blocked)
        {
            blocked = false;
            float travelDistance = velocity.magnitude * fixedDeltaTime;
            if (travelDistance <= 0f)
            {
                return velocity;
            }

            Vector2 direction = velocity / velocity.magnitude;
            int hitCount = bodyCollider.Cast(direction, collisionFilter, castHits, travelDistance);
            bool hitBlockingSurface = false;
            Vector2 adjustedVelocity = velocity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = castHits[index];
                if (!hit.collider || hit.collider == bodyCollider || hit.distance >= travelDistance) continue;
                hitBlockingSurface = true;
                float intoSurface = Vector2.Dot(adjustedVelocity, hit.normal);
                if (intoSurface < 0f) adjustedVelocity -= hit.normal * intoSurface;
            }

            if (!hitBlockingSurface)
            {
                return velocity;
            }

            if (adjustedVelocity.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon)
            {
                blocked = true;
                return Vector2.zero;
            }

            Vector2 slideDirection = adjustedVelocity.normalized;
            float slideDistance = adjustedVelocity.magnitude * fixedDeltaTime;
            int slideHitCount = bodyCollider.Cast(slideDirection, collisionFilter, castHits, slideDistance);
            float allowedDistance = slideDistance;
            for (int index = 0; index < slideHitCount; index++)
            {
                RaycastHit2D hit = castHits[index];
                if (hit.collider && hit.collider != bodyCollider)
                    allowedDistance = Mathf.Min(allowedDistance, hit.distance);
            }

            float allowedScale = slideDistance <= 0f
                ? 0f
                : Mathf.Max(0f, allowedDistance - Physics2D.defaultContactOffset) / slideDistance;
            if (allowedScale <= NavigationWorldQueries.GeometryEpsilon)
            {
                blocked = true;
                return Vector2.zero;
            }

            return adjustedVelocity * Mathf.Min(1f, allowedScale);
        }

        private static void ValidateRatio(float value, string parameterName)
        {
            if (!NavigationNumeric.IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Flexibility must be finite and within [0, 1].");
            }
        }

    }
}
