using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Contains the immutable inputs for one world-space ballistic jump.</summary>
    public readonly struct JumpTrajectoryInput
    {
        /// <summary>Gets the world-space launch position.</summary>
        public Vector2 StartPosition { get; }

        /// <summary>Gets the requested world-space landing position.</summary>
        public Vector2 LandingPosition { get; }

        /// <summary>Gets the world gravity vector before applying gravity scale.</summary>
        public Vector2 Gravity { get; }

        /// <summary>Gets the body's gravity scale.</summary>
        public float GravityScale { get; }

        /// <summary>Gets the body's linear damping retained for future fixed-step integration.</summary>
        public float LinearDamping { get; }

        /// <summary>Gets the requested apex displacement along the direction opposite gravity.</summary>
        public float JumpHeight { get; }

        /// <summary>Gets the resolved final horizontal speed available to the jump.</summary>
        public float FinalSpeed { get; }

        /// <summary>Gets the requested horizontal displacement in world units.</summary>
        public float HorizontalDisplacement => LandingPosition.x - StartPosition.x;

        /// <summary>Creates immutable trajectory inputs.</summary>
        public JumpTrajectoryInput(Vector2 startPosition, Vector2 landingPosition, Vector2 gravity,
            float gravityScale, float linearDamping, float jumpHeight, float finalSpeed)
        {
            StartPosition = startPosition;
            LandingPosition = landingPosition;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            FinalSpeed = finalSpeed;
        }
    }

    /// <summary>Represents one immutable successful ballistic jump solution.</summary>
    public sealed class JumpTrajectorySolution
    {
        private readonly float gravityMagnitude;
        private readonly float verticalDirection;

        /// <summary>Gets the world-space launch position.</summary>
        public Vector2 StartPosition { get; }

        /// <summary>Gets the world-space landing position.</summary>
        public Vector2 LandingPosition { get; }

        /// <summary>Gets the world-space apex position.</summary>
        public Vector2 ApexPosition { get; }

        /// <summary>Gets the world-space launch velocity.</summary>
        public Vector2 InitialVelocity { get; }

        /// <summary>Gets the world-space velocity at the landing time.</summary>
        public Vector2 LandingVelocity { get; }

        /// <summary>Gets the time at which the trajectory reaches its apex.</summary>
        public float ApexTime { get; }

        /// <summary>Gets the full flight time to the requested landing position.</summary>
        public float FlightDuration { get; }

        private JumpTrajectorySolution(JumpTrajectoryInput input, float gravityMagnitude,
            float verticalDirection, Vector2 apexPosition, Vector2 initialVelocity,
            Vector2 landingVelocity, float apexTime, float flightDuration)
        {
            StartPosition = input.StartPosition;
            LandingPosition = input.LandingPosition;
            ApexPosition = apexPosition;
            InitialVelocity = initialVelocity;
            LandingVelocity = landingVelocity;
            ApexTime = apexTime;
            FlightDuration = flightDuration;
            this.gravityMagnitude = gravityMagnitude;
            this.verticalDirection = verticalDirection;
        }

        /// <summary>Evaluates a clamped world-space position on this zero-damping trajectory.</summary>
        public Vector2 GetPosition(float elapsedSeconds)
        {
            float time = GetClampedTime(elapsedSeconds);
            float vertical = InitialVelocity.y * time
                - verticalDirection * 0.5f * gravityMagnitude * time * time;
            return StartPosition + new Vector2(InitialVelocity.x * time, vertical);
        }

        /// <summary>Evaluates a clamped world-space velocity on this zero-damping trajectory.</summary>
        public Vector2 GetVelocity(float elapsedSeconds)
        {
            float time = GetClampedTime(elapsedSeconds);
            return new Vector2(InitialVelocity.x,
                InitialVelocity.y - verticalDirection * gravityMagnitude * time);
        }

        /// <summary>Creates a solution from validated solver values.</summary>
        internal static JumpTrajectorySolution Create(JumpTrajectoryInput input, float gravityMagnitude,
            float verticalDirection, Vector2 apexPosition, Vector2 initialVelocity,
            Vector2 landingVelocity, float apexTime, float flightDuration)
            => new(input, gravityMagnitude, verticalDirection, apexPosition, initialVelocity,
                landingVelocity, apexTime, flightDuration);

        /// <summary>Validates and clamps an evaluation time to this flight.</summary>
        private float GetClampedTime(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            return Mathf.Min(elapsedSeconds, FlightDuration);
        }
    }

    /// <summary>Calculates zero-linear-damping ballistic trajectories without scene queries.</summary>
    public static class JumpTrajectory
    {
        private const float Tolerance = 0.000001f;

        /// <summary>Attempts to solve a physically reachable trajectory.</summary>
        public static bool TrySolve(JumpTrajectoryInput input, out JumpTrajectorySolution solution)
        {
            ValidateInput(input);
            solution = null;

            float gravity = Mathf.Abs(input.Gravity.y * input.GravityScale);
            float verticalDirection = -Mathf.Sign(input.Gravity.y);
            float initialVerticalSpeed = Mathf.Sqrt(2f * gravity * input.JumpHeight);
            float landingDisplacement = (input.LandingPosition.y - input.StartPosition.y) * verticalDirection;
            float discriminant = initialVerticalSpeed * initialVerticalSpeed - 2f * gravity * landingDisplacement;
            if (discriminant < -Tolerance)
            {
                return false;
            }

            float descentSpeed = Mathf.Sqrt(Mathf.Max(0f, discriminant));
            float flightDuration = (initialVerticalSpeed + descentSpeed) / gravity;
            float requiredHorizontalSpeed = Mathf.Abs(input.HorizontalDisplacement) / flightDuration;
            if (!IsFinite(flightDuration) || flightDuration <= Tolerance
                || requiredHorizontalSpeed > input.FinalSpeed + Tolerance)
            {
                return false;
            }

            float horizontalDirection = Mathf.Sign(input.HorizontalDisplacement);
            Vector2 initialVelocity = new(requiredHorizontalSpeed * horizontalDirection,
                verticalDirection * initialVerticalSpeed);
            Vector2 landingVelocity = new(initialVelocity.x,
                verticalDirection * (initialVerticalSpeed - gravity * flightDuration));
            float apexTime = initialVerticalSpeed / gravity;
            Vector2 acceleration = new(0f, input.Gravity.y * input.GravityScale);
            Vector2 apexPosition = input.StartPosition + initialVelocity * apexTime
                + 0.5f * acceleration * apexTime * apexTime;
            solution = JumpTrajectorySolution.Create(input, gravity, verticalDirection, apexPosition,
                initialVelocity, landingVelocity, apexTime, flightDuration);
            return true;
        }

        /// <summary>Validates the supported ballistic input domain.</summary>
        private static void ValidateInput(JumpTrajectoryInput input)
        {
            if (!IsFinite(input.StartPosition) || !IsFinite(input.LandingPosition)
                || !IsFinite(input.HorizontalDisplacement) || !IsFinite(input.Gravity)
                || !IsFinite(input.GravityScale) || input.GravityScale <= 0f
                || !IsFinite(input.LinearDamping) || input.LinearDamping < 0f
                || !IsFinite(input.JumpHeight) || input.JumpHeight <= 0f
                || !IsFinite(input.FinalSpeed) || input.FinalSpeed < 0f)
            {
                throw new ArgumentException("Jump trajectory input contains malformed values.", nameof(input));
            }

            if (Mathf.Abs(input.Gravity.x) > Tolerance || input.LinearDamping > Tolerance)
            {
                throw new NotSupportedException(
                    "The jump trajectory supports only vertical gravity without linear damping.");
            }

            if (Mathf.Abs(input.Gravity.y * input.GravityScale) <= Tolerance)
            {
                throw new ArgumentException("Jump trajectory requires nonzero effective gravity.", nameof(input));
            }
        }

        /// <summary>Returns whether both vector components are finite.</summary>
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        /// <summary>Returns whether a scalar value is finite.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
