using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Verifies the shared ballistic trajectory, executor, and FixedJump authored contract.</summary>
    public sealed class JumpTrajectoryTests
    {
        /// <summary>Verifies the solution reaches its authored landing position and velocity.</summary>
        [Test]
        public void TrySolve_ProducesConsistentLandingState()
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(2f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 10f);

            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            Assert.That(solution.GetPosition(solution.FlightDuration).x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(solution.GetPosition(solution.FlightDuration).y, Is.EqualTo(0f).Within(0.0001f));
            AssertVector(solution.GetVelocity(solution.FlightDuration), solution.LandingVelocity);
        }

        /// <summary>Verifies construction does not write physics and only the first tick launches the body.</summary>
        [Test]
        public void BallisticExecutor_WritesOnlyOnFirstTick()
        {
            JumpTrajectoryInput input = new(Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, -9.81f), 1f, 0f, 1f, 10f);
            Assert.That(JumpTrajectory.TrySolve(input, out JumpTrajectorySolution solution), Is.True);
            var host = new GameObject("ballistic-executor-test");

            try
            {
                Rigidbody2D body = host.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.mass = 2f;
                body.linearVelocity = new Vector2(-1f, -2f);
                var executor = new BallisticJumpExecutor(body, solution);

                AssertVector(body.linearVelocity, new Vector2(-1f, -2f));
                Assert.That(executor.Tick(solution.FlightDuration * 0.5f), Is.False);
                AssertVector(body.linearVelocity, solution.InitialVelocity);
                Vector2 launchedVelocity = body.linearVelocity;
                Assert.That(executor.Tick(solution.FlightDuration * 0.5f), Is.True);
                AssertVector(body.linearVelocity, launchedVelocity);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>Verifies FixedJump exposes speed composition and no authored duration.</summary>
        [Test]
        public void FixedJump_UsesSpeedFieldsWithoutJumpDuration()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(FixedJump).GetField("jumpDuration", Flags), Is.Null);
            Assert.That(typeof(FixedJump).GetField("speed", Flags)?.FieldType,
                Is.EqualTo(typeof(VariableField<float>)));
            FieldInfo modifier = typeof(FixedJump).GetField("speedModifier", Flags);
            Assert.That(modifier?.FieldType, Is.EqualTo(typeof(VariableField<float>)));

            var jump = new FixedJump();
            Assert.That(jump.speedModifier.Constant, Is.EqualTo(1f));
        }

        /// <summary>Asserts component-wise equality within fixed physics precision.</summary>
        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }
    }
}
