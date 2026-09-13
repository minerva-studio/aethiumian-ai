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
        private Vector2 previousSteeringDirection;
        private bool hasPreviousSteeringDirection;
        private Vector2 steeringTarget;
        private Vector2? preferredDirection;
        private bool completesWhenPassingWaypoint;
        private Vector2 waypointStart;
        private float completionDistance;

        /// <summary>Creates an aerial executor with explicit physics and locomotion inputs.</summary>
        public FlyTraversalExecutor(
            Rigidbody2D body,
            Collider2D bodyCollider,
            float speed,
            float flexibility,
            ContactFilter2D collisionFilter,
            IReadOnlyList<Collider2D> navigationColliders = null)
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

        /// <summary>Starts or updates the current steering action without resetting smoothing history.</summary>
        public void SetSteeringTarget(Vector2 steeringTarget, Vector2? preferredDirection = null)
        {
            ThrowIfDisposed();
            Validate.Finite(steeringTarget, nameof(steeringTarget));
            if (preferredDirection.HasValue)
            {
                Validate.Finite(preferredDirection.Value, nameof(preferredDirection));
            }

            if (!IsExecuting)
            {
                BeginExecution();
            }

            this.steeringTarget = steeringTarget;
            this.preferredDirection = preferredDirection;
            completesWhenPassingWaypoint = false;
            completionDistance = StepCompletionDistance;
        }

        /// <summary>
        /// Prepares a route waypoint. Reaching its tolerance or passing the endpoint plane ends
        /// the action; collision failure takes precedence over passing. Reuse retains steering
        /// smoothing and velocity, while the supplied start remains fixed for this action.
        /// </summary>
        public void BeginWaypoint(
            Vector2 start,
            Vector2 target,
            Vector2 preferredDirection,
            float completionDistance)
        {
            ThrowIfDisposed();
            Validate.Finite(start, nameof(start));
            Validate.Finite(target, nameof(target));
            Validate.Finite(preferredDirection, nameof(preferredDirection));
            Validate.NonNegativeFinite(completionDistance, nameof(completionDistance));
            BeginExecution();
            waypointStart = start;
            steeringTarget = target;
            this.preferredDirection = preferredDirection;
            this.completionDistance = completionDistance;
            completesWhenPassingWaypoint = true;
        }

        /// <summary>Steers with the current smoothing history; collision sliding remains a valid running step.</summary>
        protected override ExecutionResult Tick_Internal(float fixedDeltaTime)
        {
            Vector2 displacement = steeringTarget - NavigationBodyGeometry.GetCenterAnchor(navigationColliders);
            float remainingDistance = displacement.magnitude;
            if (remainingDistance <= completionDistance)
            {
                hasPreviousSteeringDirection = false;
                return ExecutionResult.Completed;
            }

            float stoppingSpeed = remainingDistance / fixedDeltaTime;
            bool usePreferredDirection = preferredDirection.HasValue
                && preferredDirection.Value.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon
                && Vector2.Dot(preferredDirection.Value, displacement) > 0f;
            Vector2 desiredDirection = usePreferredDirection
                ? preferredDirection.Value.normalized
                : displacement / remainingDistance;
            if (usePreferredDirection && (!hasPreviousSteeringDirection
                || Vector2.Dot(previousSteeringDirection, desiredDirection) < 0.999f))
            {
                float forwardSpeed = Mathf.Max(0f, Vector2.Dot(body.linearVelocity, desiredDirection));
                body.linearVelocity = desiredDirection * forwardSpeed;
                previousSteeringDirection = desiredDirection;
                hasPreviousSteeringDirection = true;
            }
            else if (!usePreferredDirection)
            {
                hasPreviousSteeringDirection = false;
            }

            Vector2 targetVelocity = desiredDirection * Mathf.Min(speed, stoppingSpeed);
            Vector2 nextVelocity = Vector2.Lerp(body.linearVelocity, targetVelocity, flexibility);
            body.linearVelocity = ClampToCollision(nextVelocity, fixedDeltaTime, out bool blocked);
            // No legal velocity is a failure of this action, not a completed waypoint.
            // The node chooses whether to replan or finish; no retry counter lives here.
            if (blocked) return ExecutionResult.Failure(ExecutionFailureReason.Obstructed);
            if (completesWhenPassingWaypoint)
            {
                Vector2 segment = steeringTarget - waypointStart;
                Vector2 center = NavigationBodyGeometry.GetCenterAnchor(navigationColliders);
                if (segment.sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                    || Vector2.Dot(center - waypointStart, segment) >= segment.sqrMagnitude)
                    return ExecutionResult.Completed;
            }
            return ExecutionResult.Running;
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

        private static float StepCompletionDistance
            => Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon;

    }
}
