using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Contains the immutable inputs for one fixed-step world-space jump.</summary>
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

        /// <summary>Gets the body's linear damping used by each fixed-step update.</summary>
        public float LinearDamping { get; }

        /// <summary>Gets the requested apex displacement along the direction opposite gravity.</summary>
        public float JumpHeight { get; }

        /// <summary>Gets the fixed-step duration used by the discrete physics model.</summary>
        public float SimulationTimeStep { get; }

        /// <summary>Gets the requested horizontal displacement in world units.</summary>
        public float HorizontalDisplacement => LandingPosition.x - StartPosition.x;

        /// <summary>Creates immutable fixed-step trajectory inputs.</summary>
        public JumpTrajectoryInput(Vector2 startPosition, Vector2 landingPosition, Vector2 gravity,
            float gravityScale, float linearDamping, float jumpHeight, float simulationTimeStep)
        {
            StartPosition = startPosition;
            LandingPosition = landingPosition;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            SimulationTimeStep = simulationTimeStep;
        }
    }

    /// <summary>Represents one immutable successful fixed-step jump solution.</summary>
    public sealed class JumpTrajectorySolution
    {
        private readonly Vector2 gravity;
        private readonly float dampingFactor;
        private readonly float simulationTimeStep;
        private readonly Vector2[] positions;
        private readonly Vector2[] velocities;

        /// <summary>Gets the world-space launch position.</summary>
        public Vector2 StartPosition { get; }

        /// <summary>Gets the world-space landing position.</summary>
        public Vector2 LandingPosition { get; }

        /// <summary>Gets the world-space apex position sampled by the discrete model.</summary>
        public Vector2 ApexPosition { get; }

        /// <summary>Gets the world-space launch velocity.</summary>
        public Vector2 InitialVelocity { get; }

        /// <summary>Gets the world-space velocity at the landing tick.</summary>
        public Vector2 LandingVelocity { get; }

        /// <summary>Gets the fixed-step time at which the sampled apex is reached.</summary>
        public float ApexTime { get; }

        /// <summary>Gets the full fixed-step flight time to the requested landing position.</summary>
        public float FlightDuration { get; }

        private JumpTrajectorySolution(JumpTrajectoryInput input, Vector2 apexPosition,
            Vector2 initialVelocity, Vector2 landingVelocity, float apexTime, float flightDuration)
        {
            StartPosition = input.StartPosition;
            LandingPosition = input.LandingPosition;
            ApexPosition = apexPosition;
            InitialVelocity = initialVelocity;
            LandingVelocity = landingVelocity;
            ApexTime = apexTime;
            FlightDuration = flightDuration;
            gravity = input.Gravity * input.GravityScale;
            dampingFactor = 1f + input.LinearDamping * input.SimulationTimeStep;
            simulationTimeStep = input.SimulationTimeStep;
            int tickCount = Mathf.Max(1, Mathf.RoundToInt(flightDuration / simulationTimeStep));
            positions = new Vector2[tickCount + 1];
            velocities = new Vector2[tickCount + 1];
            positions[0] = StartPosition;
            velocities[0] = InitialVelocity;
            for (int tick = 1; tick <= tickCount; tick++)
            {
                positions[tick] = positions[tick - 1];
                velocities[tick] = velocities[tick - 1];
                Advance(ref positions[tick], ref velocities[tick], simulationTimeStep);
            }
        }

        /// <summary>Evaluates a clamped world-space position using the same discrete damping model as the solver.</summary>
        public Vector2 GetPosition(float elapsedSeconds)
        {
            Simulate(elapsedSeconds, out Vector2 position, out _);
            return position;
        }

        /// <summary>Evaluates a clamped world-space velocity using the same discrete damping model as the solver.</summary>
        public Vector2 GetVelocity(float elapsedSeconds)
        {
            Simulate(elapsedSeconds, out _, out Vector2 velocity);
            return velocity;
        }

        /// <summary>Creates a solution from validated solver values.</summary>
        internal static JumpTrajectorySolution Create(JumpTrajectoryInput input, Vector2 apexPosition,
            Vector2 initialVelocity, Vector2 landingVelocity, float apexTime, float flightDuration)
            => new(input, apexPosition, initialVelocity, landingVelocity, apexTime, flightDuration);

        /// <summary>Reads cached fixed ticks followed by one optional partial tick.</summary>
        private void Simulate(float elapsedSeconds, out Vector2 position, out Vector2 velocity)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            float time = Mathf.Min(elapsedSeconds, FlightDuration);
            int fullTicks = Mathf.FloorToInt((time + 0.0000001f) / simulationTimeStep);
            float remainder = time - fullTicks * simulationTimeStep;
            fullTicks = Mathf.Clamp(fullTicks, 0, positions.Length - 1);
            position = positions[fullTicks];
            velocity = velocities[fullTicks];

            if (remainder > 0.0000001f)
            {
                Advance(ref position, ref velocity, remainder);
            }
        }

        /// <summary>Applies one Unity 2D-style damped fixed-step update.</summary>
        private void Advance(ref Vector2 position, ref Vector2 velocity, float timeStep)
        {
            float factor = Mathf.Approximately(timeStep, simulationTimeStep)
                ? dampingFactor
                : 1f + (dampingFactor - 1f) * timeStep / simulationTimeStep;
            velocity = (velocity + gravity * timeStep) / factor;
            position += velocity * timeStep;
        }
    }

    /// <summary>Solves bounded fixed-step jump trajectories with Unity 2D gravity and damping.</summary>
    public static class JumpTrajectory
    {
        private const float Tolerance = 0.000001f;
        private const int MinimumFlightTicks = 2;
        private const int MaximumFlightTicks = 4096;

        /// <summary>Attempts to solve a physically reachable trajectory without applying a horizontal speed cap.</summary>
        public static bool TrySolve(JumpTrajectoryInput input, out JumpTrajectorySolution solution)
            => TrySolve(input, MaximumFlightTicks, out solution);

        /// <summary>Attempts to solve a trajectory within an explicit fixed-tick budget.</summary>
        public static bool TrySolve(JumpTrajectoryInput input, int maxFlightTicks, out JumpTrajectorySolution solution)
        {
            ValidateInput(input);
            if (maxFlightTicks <= 0) throw new ArgumentOutOfRangeException(nameof(maxFlightTicks));
            solution = null;

            float gravityMagnitude = Mathf.Abs(input.Gravity.y * input.GravityScale);
            float verticalDirection = -Mathf.Sign(input.Gravity.y);
            float targetVerticalDisplacement = (input.LandingPosition.y - input.StartPosition.y) * verticalDirection;
            float dampingFactor = 1f + input.LinearDamping * input.SimulationTimeStep;

            float zeroVelocity = 0f;
            float zeroDisplacement = 0f;
            float unitVelocity = 1f;
            float unitDisplacement = 0f;
            float horizontalDisplacementFactor = 0f;
            float horizontalVelocityFactor = 1f;
            float bestDifference = float.PositiveInfinity;
            Candidate best = default;
            bool found = false;

            for (int tick = 1; tick <= maxFlightTicks; tick++)
            {
                zeroVelocity = (zeroVelocity - gravityMagnitude * input.SimulationTimeStep) / dampingFactor;
                zeroDisplacement += zeroVelocity * input.SimulationTimeStep;
                unitVelocity /= dampingFactor;
                unitDisplacement += unitVelocity * input.SimulationTimeStep;
                horizontalVelocityFactor /= dampingFactor;
                horizontalDisplacementFactor += horizontalVelocityFactor * input.SimulationTimeStep;

                if (tick < MinimumFlightTicks || unitDisplacement <= Tolerance) continue;

                float initialVerticalSpeed = (targetVerticalDisplacement - zeroDisplacement) / unitDisplacement;
                if (!IsFinite(initialVerticalSpeed) || initialVerticalSpeed <= Tolerance) continue;

                float landingVerticalVelocity = zeroVelocity + unitVelocity * initialVerticalSpeed;
                if (landingVerticalVelocity >= -Tolerance) continue;

                int apexTick = FindApexTick(input, gravityMagnitude, dampingFactor, initialVerticalSpeed, tick);
                if (apexTick <= 0 || apexTick >= tick) continue;

                float apexDisplacement = GetVerticalDisplacement(input, gravityMagnitude, dampingFactor,
                    initialVerticalSpeed, apexTick);
                if (!IsFinite(apexDisplacement) || apexDisplacement > input.JumpHeight + Tolerance) continue;

                float difference = input.JumpHeight - apexDisplacement;
                if (difference >= bestDifference) continue;

                best = new Candidate(tick, apexTick, initialVerticalSpeed, landingVerticalVelocity,
                    horizontalDisplacementFactor, horizontalVelocityFactor);
                bestDifference = difference;
                found = true;
            }

            if (!found) return false;

            float horizontalDirection = Mathf.Sign(input.HorizontalDisplacement);
            float initialHorizontalSpeed = Mathf.Abs(input.HorizontalDisplacement) / best.HorizontalDisplacementFactor;
            if (!IsFinite(initialHorizontalSpeed)) return false;

            Vector2 initialVelocity = new(initialHorizontalSpeed * horizontalDirection,
                verticalDirection * best.InitialVerticalSpeed);
            Vector2 landingVelocity = new(initialVelocity.x * best.HorizontalVelocityFactor,
                verticalDirection * best.LandingVerticalVelocity);
            float apexHorizontalDisplacement = initialVelocity.x * GetHorizontalFactor(
                input.LinearDamping, input.SimulationTimeStep, best.ApexTick);
            Vector2 apexPosition = input.StartPosition + new Vector2(
                apexHorizontalDisplacement, verticalDirection * GetVerticalDisplacement(
                    input, gravityMagnitude, dampingFactor, best.InitialVerticalSpeed, best.ApexTick));

            solution = JumpTrajectorySolution.Create(input, apexPosition, initialVelocity, landingVelocity,
                best.ApexTick * input.SimulationTimeStep, best.FlightTick * input.SimulationTimeStep);
            return true;
        }

        /// <summary>Validates the complete fixed-step trajectory input domain.</summary>
        private static void ValidateInput(JumpTrajectoryInput input)
        {
            if (!IsFinite(input.StartPosition) || !IsFinite(input.LandingPosition)
                || !IsFinite(input.Gravity) || !IsFinite(input.GravityScale) || input.GravityScale <= 0f
                || !IsFinite(input.LinearDamping) || input.LinearDamping < 0f
                || !IsFinite(input.JumpHeight) || input.JumpHeight <= 0f
                || !IsFinite(input.SimulationTimeStep) || input.SimulationTimeStep <= 0f)
            {
                throw new ArgumentException("Jump trajectory input contains malformed values.", nameof(input));
            }

            if (Mathf.Abs(input.Gravity.x) > Tolerance)
            {
                throw new NotSupportedException("The jump trajectory supports only vertical gravity.");
            }

            if (Mathf.Abs(input.Gravity.y * input.GravityScale) <= Tolerance)
            {
                throw new ArgumentException("Jump trajectory requires nonzero effective gravity.", nameof(input));
            }
        }

        /// <summary>Finds the first sampled tick at or below zero vertical velocity.</summary>
        private static int FindApexTick(JumpTrajectoryInput input, float gravityMagnitude,
            float dampingFactor, float initialVerticalSpeed, int flightTick)
        {
            float velocity = initialVerticalSpeed;
            for (int tick = 1; tick < flightTick; tick++)
            {
                velocity = (velocity - gravityMagnitude * input.SimulationTimeStep) / dampingFactor;
                if (velocity <= 0f) return tick;
            }

            return -1;
        }

        /// <summary>Returns the vertical displacement after a bounded number of fixed ticks.</summary>
        private static float GetVerticalDisplacement(JumpTrajectoryInput input, float gravityMagnitude,
            float dampingFactor, float initialVerticalSpeed, int tickCount)
        {
            float velocity = initialVerticalSpeed;
            float displacement = 0f;
            for (int tick = 0; tick < tickCount; tick++)
            {
                velocity = (velocity - gravityMagnitude * input.SimulationTimeStep) / dampingFactor;
                displacement += velocity * input.SimulationTimeStep;
            }

            return displacement;
        }

        /// <summary>Returns the horizontal displacement factor after the requested number of fixed ticks.</summary>
        private static float GetHorizontalFactor(float damping, float timeStep, int tickCount)
        {
            float dampingFactor = 1f + damping * timeStep;
            float velocityFactor = 1f;
            float displacementFactor = 0f;
            for (int tick = 0; tick < tickCount; tick++)
            {
                velocityFactor /= dampingFactor;
                displacementFactor += velocityFactor * timeStep;
            }

            return displacementFactor;
        }

        /// <summary>Returns whether both vector components are finite.</summary>
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        /// <summary>Returns whether a scalar value is finite.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct Candidate
        {
            public readonly int FlightTick;
            public readonly int ApexTick;
            public readonly float InitialVerticalSpeed;
            public readonly float LandingVerticalVelocity;
            public readonly float HorizontalDisplacementFactor;
            public readonly float HorizontalVelocityFactor;

            public Candidate(int flightTick, int apexTick, float initialVerticalSpeed,
                float landingVerticalVelocity, float horizontalDisplacementFactor,
                float horizontalVelocityFactor)
            {
                FlightTick = flightTick;
                ApexTick = apexTick;
                InitialVerticalSpeed = initialVerticalSpeed;
                LandingVerticalVelocity = landingVerticalVelocity;
                HorizontalDisplacementFactor = horizontalDisplacementFactor;
                HorizontalVelocityFactor = horizontalVelocityFactor;
            }
        }
    }
}
