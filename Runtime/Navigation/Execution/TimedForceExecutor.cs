using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Executes one bounded continuous-force maneuver from a fixed-update owner.</summary>
    public sealed class TimedForceExecutor : MovementExecutor
    {
        private readonly Rigidbody2D body;
        private readonly Vector2 continuousForce;
        private readonly ForceMode2D forceMode;
        private readonly float duration;

        private float elapsed;

        /// <summary>Creates a bounded maneuver that applies the supplied force on every active fixed tick.</summary>
        public TimedForceExecutor(Rigidbody2D body, Vector2 continuousForce, ForceMode2D forceMode, float duration)
        {
            if (!body) throw new ArgumentNullException(nameof(body));
            Validate.Finite(continuousForce, nameof(continuousForce));
            Validate.NonNegativeFinite(duration, nameof(duration));

            this.body = body;
            this.continuousForce = continuousForce;
            this.forceMode = forceMode;
            this.duration = duration;
            BeginExecution();
        }

        /// <summary>Applies force for active steps only. A zero-duration action completes without a physics write.</summary>
        protected override ExecutionResult Tick_Internal(float fixedDeltaTime)
        {
            if (elapsed >= duration)
            {
                return ExecutionResult.Completed;
            }

            body.AddForce(continuousForce, forceMode);

            elapsed = Mathf.Min(duration, elapsed + fixedDeltaTime);
            return elapsed >= duration ? ExecutionResult.Completed : ExecutionResult.Running;
        }


    }
}
