using Aethiumian.AI.Navigation;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Verifies the shared ballistic trajectory solver and cache contracts.</summary>
    public sealed class JumpTrajectoryTests
    {
        /// <summary>Verifies the solution reaches its authored landing position and velocity.</summary>
        [Test]
        public void TrySolve_ProducesConsistentLandingState()
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(2f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 0.02f);

            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.GetPosition(solution.FlightDuration).x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).y, Is.EqualTo(0f).Within(0.0001f));
            AssertVector(solution.GetVelocity(solution.FlightDuration), solution.LandingVelocity);
        }

        /// <summary>Verifies positive authored height uses exactly 0.25 world units of effective apex headroom.</summary>
        [Test]
        public void ApexPolicy_UsesPositiveHeightHeadroom()
        {
            Assert.That(JumpTrajectory.GetMaximumAllowedApexHeight(2.99f), Is.EqualTo(3.24f).Within(0.0001f));
            Assert.That(JumpTrajectory.IsApexHeightAllowed(2.99f, 3.24f), Is.True);
            Assert.That(JumpTrajectory.IsApexHeightAllowed(2.99f, 3.2401f), Is.False);
        }

        /// <summary>Verifies zero authored height remains a zero apex maximum.</summary>
        [Test]
        public void ApexPolicy_ZeroHeightRemainsZero()
        {
            Assert.That(JumpTrajectory.GetMaximumAllowedApexHeight(0f), Is.EqualTo(0f));
            Assert.That(JumpTrajectory.IsApexHeightAllowed(0f, 0f), Is.True);
            Assert.That(JumpTrajectory.IsApexHeightAllowed(0f, 0.0001f), Is.False);
        }

        /// <summary>Verifies malformed authored and sampled apex values are rejected.</summary>
        [Test]
        public void ApexPolicy_RejectsNonFiniteValues()
        {
            Assert.That(() => JumpTrajectory.GetMaximumAllowedApexHeight(float.NaN),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => JumpTrajectory.GetMaximumAllowedApexHeight(float.PositiveInfinity),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(JumpTrajectory.IsApexHeightAllowed(2f, float.NaN), Is.False);
            Assert.That(JumpTrajectory.IsApexHeightAllowed(2f, float.PositiveInfinity), Is.False);
        }

        /// <summary>Verifies the solver selects the lowest apex that satisfies an explicit minimum.</summary>
        [Test]
        public void TrySolve_UsesMinimumApexWithoutExceedingMaximum()
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(2f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 0.02f);

            Assert.That(JumpTrajectory.TrySolve(input, 4096, 0.75f, out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.ApexPosition.y, Is.GreaterThanOrEqualTo(0.75f - 0.0001f));
            Assert.That(solution.ApexPosition.y, Is.LessThanOrEqualTo(2.0001f));
        }

        /// <summary>Verifies explicit minimum apex validation retains its public exception contract.</summary>
        [TestCase(-0.001f)]
        [TestCase(2.251f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TrySolve_RejectsInvalidMinimumApexHeight(float minimumApexHeight)
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(2f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 0.02f);

            Assert.That(() => JumpTrajectory.TrySolve(input, 4096, minimumApexHeight, out _),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }


        /// <summary>Verifies damped positions and velocities follow the Unity fixed-step recurrence.</summary>
        [Test]
        public void TrySolve_UsesDiscreteDampingRecurrence()
        {
            const float timeStep = 0.02f;
            const float damping = 5f;
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, -9.81f), 4f, damping, 2f, timeStep);

            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            Vector2 position = input.StartPosition;
            Vector2 velocity = solution.InitialVelocity;
            float dampingFactor = 1f + damping * timeStep;
            float sampleTime = timeStep * 3f;
            for (int tick = 0; tick < 3; tick++)
            {
                velocity = (velocity + input.Gravity * input.GravityScale * timeStep) / dampingFactor;
                position += velocity * timeStep;
            }

            AssertVector(solution.GetPosition(sampleTime), position);
            AssertVector(solution.GetVelocity(sampleTime), velocity);
            Assert.That(solution.GetPosition(solution.FlightDuration).y, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>Verifies compact solutions retain no per-tick position or velocity arrays.</summary>
        [Test]
        public void TrySolve_SolutionStoresCompactRecurrence()
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, -9.81f), 4f, 5f, 2f, 0.02f);

            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            FieldInfo[] fields = solution.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(Array.Exists(fields, field => field.FieldType.IsArray), Is.False);
        }

        /// <summary>Verifies partial-tick evaluation follows the existing damped recurrence contract.</summary>
        [Test]
        public void TrySolve_CompactSolutionPreservesPartialTickRecurrence()
        {
            const float timeStep = 0.02f;
            const float partialStep = 0.007f;
            const float damping = 5f;
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, -9.81f), 4f, damping, 2f, timeStep);
            Assert.That(JumpTrajectory.TrySolve(input, 4096, 1f, out JumpTrajectorySolution solution), Is.True);
            Vector2 position = input.StartPosition;
            Vector2 velocity = solution.InitialVelocity;
            float dampingFactor = 1f + damping * timeStep;

            for (int tick = 0; tick < 3; tick++)
            {
                velocity = (velocity + input.Gravity * input.GravityScale * timeStep) / dampingFactor;
                position += velocity * timeStep;
            }

            float partialDampingFactor = 1f + damping * partialStep;
            velocity = (velocity + input.Gravity * input.GravityScale * partialStep) / partialDampingFactor;
            position += velocity * partialStep;

            AssertVector(solution.GetPosition(timeStep * 3f + partialStep), position);
            AssertVector(solution.GetVelocity(timeStep * 3f + partialStep), velocity);
        }

        /// <summary>Verifies malformed fixed-step durations fail at the trajectory input boundary.</summary>
        [Test]
        public void TrySolve_RejectsInvalidSimulationTimeStep()
        {
            Assert.That(() => JumpTrajectory.TrySolve(
                new JumpTrajectoryInput(Vector2.zero, Vector2.right, Vector2.down, 1f, 0f, 1f, 0f), out _),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(typeof(JumpTrajectoryInput).GetProperty("FinalSpeed", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        /// <summary>Verifies explicit solve budgets cannot exceed the compact solver's bounded workspace.</summary>
        [Test]
        public void TrySolve_RejectsFlightBudgetAboveHardLimit()
        {
            JumpTrajectoryInput input = new(Vector2.zero, Vector2.right,
                new Vector2(0f, -9.81f), 1f, 0f, 1f, 0.02f);

            Assert.That(() => JumpTrajectory.TrySolve(input, 4097, out _),
                Throws.InstanceOf<ArgumentException>());
            Assert.DoesNotThrow(() => JumpTrajectory.TrySolve(input, 4096, out _));
        }

        [Test]
        public void SolvesReachableJumpWithinApexBounds()
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(2, 0), 2, 5), out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.ApexPosition.y, Is.LessThanOrEqualTo(2.0001f));
            Assert.That(solution.ApexPosition.y, Is.GreaterThanOrEqualTo(1f - 0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).x, Is.EqualTo(2).Within(0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).y, Is.EqualTo(0).Within(0.0001f));
        }

        [Test]
        public void SupportsZeroHorizontalDisplacement()
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(0f, 1f), 2f, 0f), out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.InitialVelocity.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void UsesVisibleDefaultFloorAndAllowsExplicitLowArc()
        {
            JumpTrajectoryInput input = CreateInput(Vector2.zero, new Vector2(2f, 0f), 4f, 5f);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution defaultSolution), Is.True);
            Assert.That(defaultSolution.ApexPosition.y, Is.GreaterThanOrEqualTo(2f - 0.0001f));
            Assert.That(defaultSolution.ApexPosition.y, Is.LessThanOrEqualTo(4.0001f));
            Assert.That(JumpTrajectory.TrySolve(input, 4096, 0f, out JumpTrajectorySolution lowSolution), Is.True);
            Assert.That(lowSolution.ApexPosition.y, Is.LessThan(0.1f));
        }

        [Test]
        public void ClampsDefaultFloorToSmallMaximum()
        {
            JumpTrajectoryInput input = CreateInput(Vector2.zero, new Vector2(2f, 0f), 0.5f, 5f);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.ApexPosition.y, Is.GreaterThanOrEqualTo(0.25f - 0.0001f));
            Assert.That(solution.ApexPosition.y, Is.LessThanOrEqualTo(0.5001f));
        }

        [Test]
        public void SupportsHigherAndLowerLanding()
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(2, 1), 2, 5), out JumpTrajectorySolution higher), Is.True);
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(2, -2), 2, 5), out JumpTrajectorySolution lower), Is.True);
            Assert.That(higher.GetPosition(higher.FlightDuration).y, Is.EqualTo(1).Within(0.0001f));
            Assert.That(lower.GetPosition(lower.FlightDuration).y, Is.EqualTo(-2).Within(0.0001f));
        }

        [Test]
        public void RejectsExcessiveHeightAndLeavesHorizontalCapToPlanner()
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(2, 3), 2, 5), out JumpTrajectorySolution solution), Is.False);
            Assert.That(solution, Is.Null);
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(100, 0), 2, 1), out solution), Is.True);
            Assert.That(solution, Is.Not.Null);
        }

        [Test]
        public void IsSymmetricForMirroredInputs()
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(2, 1), 2, 5), out JumpTrajectorySolution right), Is.True);
            Assert.That(JumpTrajectory.TrySolve(CreateInput(Vector2.zero, new Vector2(-2, 1), 2, 5), out JumpTrajectorySolution left), Is.True);
            Assert.That(left.FlightDuration, Is.EqualTo(right.FlightDuration).Within(0.0001f));
            Assert.That(left.GetPosition(left.FlightDuration * 0.5f).x, Is.EqualTo(-right.GetPosition(right.FlightDuration * 0.5f).x).Within(0.0001f));
            Assert.That(left.GetPosition(left.FlightDuration * 0.5f).y, Is.EqualTo(right.GetPosition(right.FlightDuration * 0.5f).y).Within(0.0001f));
        }

        [Test]
        public void IsDeterministicForRepeatedSolves()
        {
            JumpTrajectoryInput input = CreateInput(Vector2.zero, new Vector2(2, 1), 2, 5);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution first), Is.True);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution second), Is.True);
            Assert.That(second.InitialVelocity, Is.EqualTo(first.InitialVelocity));
            Assert.That(second.LandingVelocity, Is.EqualTo(first.LandingVelocity));
            Assert.That(second.FlightDuration, Is.EqualTo(first.FlightDuration));
        }

        [Test]
        public void RejectsMalformedInput()
        {
            Assert.That(() => JumpTrajectory.TrySolve(CreateInput(Vector2.zero, Vector2.right, -1, 5), out _), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => JumpTrajectory.TrySolve(default, out _), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the discrete recurrence supports a damped body.</summary>
        [Test]
        public void AcceptsDiscreteLinearDamping()
        {
            JumpTrajectoryInput damped = new(Vector2.zero, Vector2.right, new Vector2(0f, -9.81f), 1f, 0.1f, 2f, 0.02f);

            Assert.That(JumpTrajectory.TrySolve(damped, out _), Is.True);
        }

        /// <summary>Verifies a profile without authored height or effective gravity reports no trajectory.</summary>
        [Test]
        public void ReportsNoTrajectoryWithoutHeightOrEffectiveGravity()
        {
            JumpTrajectoryInput noHeight = new(Vector2.zero, Vector2.right, new Vector2(0f, -9.81f), 1f, 0f, 0f, 0.02f);
            JumpTrajectoryInput noGravity = new(Vector2.zero, Vector2.right, Vector2.zero, 1f, 0f, 2f, 0.02f);

            Assert.That(JumpTrajectory.TrySolve(noHeight, out JumpTrajectorySolution solution), Is.False);
            Assert.That(solution, Is.Null);
            Assert.That(JumpTrajectory.TrySolve(noGravity, out solution), Is.False);
            Assert.That(solution, Is.Null);
        }

        [Test]
        public void CacheRetainsDeterministicRejectionAndEvictsLeastRecentlyUsedEntry()
        {
            JumpTrajectoryCache cache = new(2);
            GroundJumpParameters parameters = new(new Vector2(0.8f, 1.5f), new Vector2(0f, -9.81f), 1f, 0f, 2f, 4f, 0.02f);
            Vector2 start = new(1f, 1f);
            Vector2 landing = new(3f, 1f);
            cache.Publish(start, landing, parameters, null);
            Assert.That(cache.TryGet(start, landing, parameters, out JumpTrajectorySolution rejected), Is.True);
            Assert.That(rejected, Is.Null);
            cache.Publish(start, new Vector2(4f, 1f), parameters, null);
            Assert.That(cache.TryGet(start, landing, parameters, out _), Is.True);
            cache.Publish(start, new Vector2(5f, 1f), parameters, null);
            Assert.That(cache.TryGet(start, new Vector2(4f, 1f), parameters, out _), Is.False);
        }

        private static JumpTrajectoryInput CreateInput(Vector2 start, Vector2 landing, float height, float unusedSpeed)
            => new(start, landing, new Vector2(0, -9.81f), 1, 0, height, 0.02f);

        /// <summary>Asserts component-wise equality within fixed physics precision.</summary>
        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }
    }
}
