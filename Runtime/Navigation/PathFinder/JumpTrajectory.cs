using System;
using System.Buffers;
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
        private readonly int flightTickCount;

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
            flightTickCount = Mathf.Max(1, Mathf.RoundToInt(flightDuration / simulationTimeStep));
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

        /// <summary>Evaluates the fixed-step recurrence followed by one optional partial tick.</summary>
        private void Simulate(float elapsedSeconds, out Vector2 position, out Vector2 velocity)
        {
            if (!NavigationNumeric.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            float time = Mathf.Min(elapsedSeconds, FlightDuration);
            int fullTicks = Mathf.FloorToInt((time + 0.0000001f) / simulationTimeStep);
            float remainder = time - fullTicks * simulationTimeStep;
            fullTicks = Mathf.Clamp(fullTicks, 0, flightTickCount);
            EvaluateFullTicks(fullTicks, out position, out velocity);

            if (remainder > 0.0000001f)
            {
                Advance(ref position, ref velocity, remainder);
            }
        }

        /// <summary>Evaluates complete fixed ticks from the compact damped recurrence.</summary>
        private void EvaluateFullTicks(int tickCount, out Vector2 position, out Vector2 velocity)
        {
            if (tickCount == 0)
            {
                position = StartPosition;
                velocity = InitialVelocity;
                return;
            }

            if (dampingFactor == 1f)
            {
                float tickTime = tickCount * simulationTimeStep;
                velocity = InitialVelocity + gravity * tickTime;
                float acceleratedTickSum = tickCount * (tickCount + 1f) * 0.5f;
                position = StartPosition
                    + InitialVelocity * tickTime
                    + gravity * (simulationTimeStep * simulationTimeStep * acceleratedTickSum);
                return;
            }

            double inverseDamping = 1d / dampingFactor;
            double velocityPower = Math.Pow(inverseDamping, tickCount);
            double velocitySumFactor = inverseDamping * (1d - velocityPower) / (1d - inverseDamping);
            velocity = new Vector2(
                EvaluateVelocity(InitialVelocity.x, gravity.x, velocityPower),
                EvaluateVelocity(InitialVelocity.y, gravity.y, velocityPower));
            position = new Vector2(
                EvaluatePosition(StartPosition.x, InitialVelocity.x, gravity.x, tickCount, velocitySumFactor),
                EvaluatePosition(StartPosition.y, InitialVelocity.y, gravity.y, tickCount, velocitySumFactor));
        }

        /// <summary>Evaluates one velocity component after complete fixed ticks.</summary>
        private float EvaluateVelocity(float initialVelocity, float acceleration, double velocityPower)
        {
            double gravityStep = acceleration * simulationTimeStep;
            double terminalOffset = gravityStep / (dampingFactor - 1d);
            return (float)(velocityPower * (initialVelocity - terminalOffset) + terminalOffset);
        }

        /// <summary>Evaluates one position component after complete fixed ticks.</summary>
        private float EvaluatePosition(float startPosition, float initialVelocity, float acceleration,
            int tickCount, double velocitySumFactor)
        {
            double gravityStep = acceleration * simulationTimeStep;
            double terminalOffset = gravityStep / (dampingFactor - 1d);
            double velocitySum = (initialVelocity - terminalOffset) * velocitySumFactor
                + tickCount * terminalOffset;
            return (float)(startPosition + simulationTimeStep * velocitySum);
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
        private const float DefaultMinimumApexRatio = 0.5f;

        private const float MaximumApexHeadroom = 0.25f;
        private const float DefaultMaximumApexWorldHeight = 2f;
        private const int MinimumFlightTicks = 2;
        private const int MaximumFlightTicks = 4096;

        /// <summary>Gets the maximum sampled apex displacement allowed for an authored jump height.</summary>
        public static float GetMaximumAllowedApexHeight(float jumpHeight)
        {
            ValidateAuthoredJumpHeight(jumpHeight);
            return jumpHeight <= 0f ? 0f : jumpHeight + MaximumApexHeadroom;
        }

        /// <summary>Returns whether a sampled apex is within the authored height policy.</summary>
        public static bool IsApexHeightAllowed(float jumpHeight, float apexHeight)
        {
            ValidateAuthoredJumpHeight(jumpHeight);
            return NavigationNumeric.IsFinite(apexHeight) && apexHeight >= 0f
                && apexHeight <= GetMaximumAllowedApexHeight(jumpHeight) + Tolerance;
        }

        /// <summary>Gets the default visible jump floor without applying headroom.</summary>
        public static float GetDefaultMinimumApexHeight(float jumpHeight)
        {
            ValidateAuthoredJumpHeight(jumpHeight);
            if (jumpHeight <= Tolerance) return jumpHeight;
            return Mathf.Min(jumpHeight,
                Mathf.Min(jumpHeight * DefaultMinimumApexRatio, DefaultMaximumApexWorldHeight));
        }

        private static void ValidateAuthoredJumpHeight(float jumpHeight)
        {
            if (!NavigationNumeric.IsFinite(jumpHeight) || jumpHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(jumpHeight));
        }

        /// <summary>Attempts to solve a physically reachable trajectory without applying a horizontal speed cap.</summary>
        public static bool TrySolve(JumpTrajectoryInput input, out JumpTrajectorySolution solution)
        {
            ValidateInput(input);
            return TrySolve(input, MaximumFlightTicks, GetDefaultMinimumApexHeight(input.JumpHeight), out solution);
        }

        /// <summary>Attempts to solve a trajectory within an explicit fixed-tick budget.</summary>
        public static bool TrySolve(JumpTrajectoryInput input, int maxFlightTicks, out JumpTrajectorySolution solution)
        {
            ValidateInput(input);
            return TrySolve(input, maxFlightTicks, GetDefaultMinimumApexHeight(input.JumpHeight), out solution);
        }

        /// <summary>Attempts to solve the lowest trajectory whose sampled apex reaches the requested minimum height.</summary>
        /// <param name="input">The physical jump inputs; <see cref="JumpTrajectoryInput.JumpHeight"/> is the authored nominal height, with the effective maximum provided by <see cref="GetMaximumAllowedApexHeight(float)"/>.</param>
        /// <param name="maxFlightTicks">The maximum fixed-step flight duration.</param>
        /// <param name="minimumApexHeight">The minimum apex displacement above launch, in world units.</param>
        /// <param name="solution">The lowest valid solution, when one exists.</param>
        /// <returns>True when a trajectory exists between the requested minimum and effective maximum apex heights.</returns>
        public static bool TrySolve(JumpTrajectoryInput input, int maxFlightTicks, float minimumApexHeight,
            out JumpTrajectorySolution solution)
        {
            ValidateInput(input);
            if (maxFlightTicks <= 0 || maxFlightTicks > MaximumFlightTicks)
                throw new ArgumentOutOfRangeException(nameof(maxFlightTicks),
                    $"Flight ticks must be between 1 and {MaximumFlightTicks}.");
            if (!IsApexHeightAllowed(input.JumpHeight, minimumApexHeight))
                throw new ArgumentOutOfRangeException(nameof(minimumApexHeight));
            solution = null;

            float gravityMagnitude = Mathf.Abs(input.Gravity.y * input.GravityScale);
            float verticalDirection = -Mathf.Sign(input.Gravity.y);
            float targetVerticalDisplacement = (input.LandingPosition.y - input.StartPosition.y) * verticalDirection;
            float dampingFactor = 1f + input.LinearDamping * input.SimulationTimeStep;

            int sampleCount = maxFlightTicks + 1;
            ArrayPool<float> pool = ArrayPool<float>.Shared;
            float[] zeroVelocities = null;
            float[] zeroDisplacements = null;
            float[] unitVelocities = null;
            float[] unitDisplacements = null;
            try
            {
                zeroVelocities = pool.Rent(sampleCount);
                zeroDisplacements = pool.Rent(sampleCount);
                unitVelocities = pool.Rent(sampleCount);
                unitDisplacements = pool.Rent(sampleCount);
                zeroVelocities[0] = 0f;
                zeroDisplacements[0] = 0f;
                unitVelocities[0] = 1f;
                unitDisplacements[0] = 0f;
                float bestApex = float.PositiveInfinity;
                int bestFlightTick = int.MaxValue;
                Candidate best = default;
                bool found = false;

                for (int tick = 1; tick <= maxFlightTicks; tick++)
                {
                    zeroVelocities[tick] = (zeroVelocities[tick - 1] - gravityMagnitude * input.SimulationTimeStep) / dampingFactor;
                    zeroDisplacements[tick] = zeroDisplacements[tick - 1] + zeroVelocities[tick] * input.SimulationTimeStep;
                    unitVelocities[tick] = unitVelocities[tick - 1] / dampingFactor;
                    unitDisplacements[tick] = unitDisplacements[tick - 1] + unitVelocities[tick] * input.SimulationTimeStep;
                }

                for (int tick = 1; tick <= maxFlightTicks; tick++)
                {
                    float unitDisplacement = unitDisplacements[tick];
                    if (tick < MinimumFlightTicks || unitDisplacement <= Tolerance) continue;

                    float initialVerticalSpeed = (targetVerticalDisplacement - zeroDisplacements[tick]) / unitDisplacement;
                    if (!NavigationNumeric.IsFinite(initialVerticalSpeed) || initialVerticalSpeed <= Tolerance) continue;

                    float landingVerticalVelocity = zeroVelocities[tick] + unitVelocities[tick] * initialVerticalSpeed;
                    if (landingVerticalVelocity >= -Tolerance) continue;

                    int apexTick = FindApexTick(zeroVelocities, unitVelocities, initialVerticalSpeed, tick);
                    if (apexTick <= 0 || apexTick >= tick) continue;

                    float apexDisplacement = zeroDisplacements[apexTick] + unitDisplacements[apexTick] * initialVerticalSpeed;
                    if (!IsApexHeightAllowed(input.JumpHeight, apexDisplacement)
                        || apexDisplacement < minimumApexHeight - Tolerance) continue;

                    if (apexDisplacement > bestApex + Tolerance
                        || Mathf.Abs(apexDisplacement - bestApex) <= Tolerance && tick >= bestFlightTick) continue;

                    best = new Candidate(tick, apexTick, initialVerticalSpeed, landingVerticalVelocity,
                        unitDisplacement, unitVelocities[tick], apexDisplacement);
                    bestApex = apexDisplacement;
                    bestFlightTick = tick;
                    found = true;
                }

                if (!found) return false;

                float horizontalDirection = Mathf.Sign(input.HorizontalDisplacement);
                float initialHorizontalSpeed = Mathf.Abs(input.HorizontalDisplacement) / best.HorizontalDisplacementFactor;
                if (!NavigationNumeric.IsFinite(initialHorizontalSpeed)) return false;

                Vector2 initialVelocity = new(initialHorizontalSpeed * horizontalDirection,
                    verticalDirection * best.InitialVerticalSpeed);
                Vector2 landingVelocity = new(initialVelocity.x * best.HorizontalVelocityFactor,
                    verticalDirection * best.LandingVerticalVelocity);
                float apexHorizontalDisplacement = initialVelocity.x * GetHorizontalFactor(
                    input.LinearDamping, input.SimulationTimeStep, best.ApexTick);
                Vector2 apexPosition = input.StartPosition + new Vector2(
                    apexHorizontalDisplacement, verticalDirection * best.ApexDisplacement);

                solution = JumpTrajectorySolution.Create(input, apexPosition, initialVelocity, landingVelocity,
                    best.ApexTick * input.SimulationTimeStep, best.FlightTick * input.SimulationTimeStep);
                return true;
            }
            finally
            {
                if (zeroVelocities != null) pool.Return(zeroVelocities);
                if (zeroDisplacements != null) pool.Return(zeroDisplacements);
                if (unitVelocities != null) pool.Return(unitVelocities);
                if (unitDisplacements != null) pool.Return(unitDisplacements);
            }
        }

        /// <summary>Validates the complete fixed-step trajectory input domain.</summary>
        private static void ValidateInput(JumpTrajectoryInput input)
        {
            if (!NavigationNumeric.IsFinite(input.StartPosition) || !NavigationNumeric.IsFinite(input.LandingPosition)
                || !NavigationNumeric.IsFinite(input.Gravity) || !NavigationNumeric.IsFinite(input.GravityScale) || input.GravityScale <= 0f
                || !NavigationNumeric.IsFinite(input.LinearDamping) || input.LinearDamping < 0f
                || !NavigationNumeric.IsFinite(input.JumpHeight) || input.JumpHeight <= 0f
                || !NavigationNumeric.IsFinite(input.SimulationTimeStep) || input.SimulationTimeStep <= 0f)
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
        private static int FindApexTick(float[] zeroVelocities, float[] unitVelocities,
            float initialVerticalSpeed, int flightTick)
        {
            int low = 1;
            int high = flightTick - 1;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                float velocity = zeroVelocities[middle] + unitVelocities[middle] * initialVerticalSpeed;
                if (velocity <= 0f) high = middle;
                else low = middle + 1;
            }

            return zeroVelocities[low] + unitVelocities[low] * initialVerticalSpeed <= 0f ? low : -1;
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

        private readonly struct Candidate
        {
            public readonly int FlightTick;
            public readonly int ApexTick;
            public readonly float InitialVerticalSpeed;
            public readonly float LandingVerticalVelocity;
            public readonly float HorizontalDisplacementFactor;
            public readonly float HorizontalVelocityFactor;
            public readonly float ApexDisplacement;

            public Candidate(int flightTick, int apexTick, float initialVerticalSpeed,
                float landingVerticalVelocity, float horizontalDisplacementFactor,
                float horizontalVelocityFactor, float apexDisplacement)
            {
                FlightTick = flightTick;
                ApexTick = apexTick;
                InitialVerticalSpeed = initialVerticalSpeed;
                LandingVerticalVelocity = landingVerticalVelocity;
                HorizontalDisplacementFactor = horizontalDisplacementFactor;
                HorizontalVelocityFactor = horizontalVelocityFactor;
                ApexDisplacement = apexDisplacement;
            }
        }
    }
}
