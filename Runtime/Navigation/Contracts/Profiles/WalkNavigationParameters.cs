using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable physical inputs used to plan composite walking traversal. The request body AABB supplies position and size.
    /// </summary>
    public readonly struct WalkNavigationParameters
    {
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
        public WalkNavigationParameters(float speed, Vector2 gravity, float gravityScale, float linearDamping, float jumpHeight, float jumpLength, float simulationTimeStep)
        {
            Validate.NonNegativeFinite(speed, nameof(speed));
            Validate.Finite(gravity, nameof(gravity));
            Validate.NonNegativeFinite(gravityScale, nameof(gravityScale));
            Validate.NonNegativeFinite(linearDamping, nameof(linearDamping));
            Validate.NonNegativeFinite(jumpHeight, nameof(jumpHeight));
            Validate.NonNegativeFinite(jumpLength, nameof(jumpLength));
            Validate.PositiveFinite(simulationTimeStep, nameof(simulationTimeStep));
            Speed = speed;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            JumpLength = jumpLength;
            SimulationTimeStep = simulationTimeStep;
        }

        /// <summary>
        /// Creates <see cref="GroundJumpParameters"/> using the request body's size and this profile's physical inputs.
        /// </summary>
        /// <param name="bodySize">The size derived from the request's body AABB.</param>
        public GroundJumpParameters GetJumpParameters(Vector2 bodySize)
        {
            return new(bodySize, Gravity, GravityScale, LinearDamping, JumpHeight, JumpLength, SimulationTimeStep);
        }

    }

}
