using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable physical inputs used to plan composite walking traversal.
    /// </summary>
    public readonly struct WalkNavigationParameters
    {
        /// <summary>Gets the agent body size in world units.</summary>
        public Vector2 BodySize { get; }

        /// <summary>Gets the resolved final horizontal movement speed.</summary>
        public float Speed { get; }

        /// <summary>Gets the resolved world gravity.</summary>
        public Vector2 Gravity { get; }

        /// <summary>Gets the body's gravity scale.</summary>
        public float GravityScale { get; }

        /// <summary>Gets the body's linear damping, retained for a later fixed-step physics model.</summary>
        public float LinearDamping { get; }

        /// <summary>Gets the maximum requested jump height in world units.</summary>
        public float JumpHeight { get; }

        /// <summary>Gets the maximum requested horizontal jump length in world units.</summary>
        public float JumpLength { get; }

        /// <summary>Gets the fixed-step duration used by jump trajectory simulation.</summary>
        public float SimulationTimeStep { get; }

        /// <summary>Creates and validates immutable walking navigation parameters.</summary>
        public WalkNavigationParameters(Vector2 bodySize, float speed, Vector2 gravity, float gravityScale, float linearDamping, float jumpHeight, float jumpLength, float simulationTimeStep)
        {
            Validate.PositiveVector(bodySize, nameof(bodySize));
            Validate.NonNegativeFinite(speed, nameof(speed));
            Validate.Finite(gravity, nameof(gravity));
            Validate.NonNegativeFinite(gravityScale, nameof(gravityScale));
            Validate.NonNegativeFinite(linearDamping, nameof(linearDamping));
            Validate.NonNegativeFinite(jumpHeight, nameof(jumpHeight));
            Validate.NonNegativeFinite(jumpLength, nameof(jumpLength));
            Validate.PositiveFinite(simulationTimeStep, nameof(simulationTimeStep));
            BodySize = bodySize;
            Speed = speed;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            JumpLength = jumpLength;
            SimulationTimeStep = simulationTimeStep;
        }

        /// <summary>
        /// Creates a <see cref="GroundJumpParameters"/> from this profile.
        /// </summary>
        public GroundJumpParameters GetJumpParameters()
        {
            return new(BodySize, Gravity, GravityScale, LinearDamping, JumpHeight, JumpLength, SimulationTimeStep);
        }

    }

}
