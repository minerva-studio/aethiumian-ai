using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Executes one ballistic launch whose timing comes from a trajectory solution.</summary>
    public sealed class BallisticJumpExecutor
    {
        private readonly Rigidbody2D body;
        private readonly JumpTrajectorySolution trajectory;
        private float elapsed;
        private bool launched;

        /// <summary>Creates a ballistic maneuver from one authoritative trajectory solution.</summary>
        public BallisticJumpExecutor(Rigidbody2D body, JumpTrajectorySolution trajectory)
        {
            this.body = body ? body : throw new ArgumentNullException(nameof(body));
            this.trajectory = trajectory ?? throw new ArgumentNullException(nameof(trajectory));
        }

        /// <summary>Launches on the first fixed tick and returns true when the flight duration elapses.</summary>
        public bool Tick(float fixedDeltaTime)
        {
            ValidateFixedDeltaTime(fixedDeltaTime);
            if (elapsed >= trajectory.FlightDuration)
            {
                return true;
            }

            if (!launched)
            {
                body.AddForce(body.mass * (trajectory.InitialVelocity - body.linearVelocity), ForceMode2D.Impulse);
                launched = true;
            }

            elapsed = Mathf.Min(trajectory.FlightDuration, elapsed + fixedDeltaTime);
            return elapsed >= trajectory.FlightDuration;
        }

        /// <summary>Validates one fixed-step duration.</summary>
        private static void ValidateFixedDeltaTime(float fixedDeltaTime)
        {
            if (float.IsNaN(fixedDeltaTime) || float.IsInfinity(fixedDeltaTime) || fixedDeltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime), fixedDeltaTime,
                    "Fixed delta time must be finite and positive.");
            }
        }
    }
}
