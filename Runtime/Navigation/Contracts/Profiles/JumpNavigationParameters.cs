using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Immutable physical inputs used to plan jump-only traversal.</summary>
    public readonly struct JumpNavigationParameters
    {
        /// <summary>Gets the agent body size in world units.</summary>
        public Vector2 BodySize { get; }

        /// <summary>Gets the resolved world gravity.</summary>
        public Vector2 Gravity { get; }

        /// <summary>Gets the body's gravity scale.</summary>
        public float GravityScale { get; }

        /// <summary>Gets the body's linear damping.</summary>
        public float LinearDamping { get; }

        /// <summary>Gets the maximum requested jump height in world units.</summary>
        public float JumpHeight { get; }

        /// <summary>Gets the maximum requested horizontal jump length in world units.</summary>
        public float JumpLength { get; }

        /// <summary>Gets the fixed-step duration used by jump trajectory simulation.</summary>
        public float SimulationTimeStep { get; }
        /// <summary>Gets the main-thread-captured support contact distance.</summary>
        public float SupportSnapDistance { get; }

        /// <summary>Gets the main-thread-captured ground contact tolerance used by clearance queries.</summary>
        public float GroundContactTolerance { get; }

        /// <summary>Creates and validates immutable jump-only navigation parameters.</summary>
        public JumpNavigationParameters(Vector2 bodySize, Vector2 gravity, float gravityScale, float linearDamping, float jumpHeight, float jumpLength, float simulationTimeStep)
        {
            Validate.PositiveVector(bodySize, nameof(bodySize));
            if (!NavigationNumeric.IsFinite(gravity)) throw new ArgumentException("Gravity must be finite.", nameof(gravity));
            Validate.NonNegativeFinite(gravityScale, nameof(gravityScale));
            Validate.NonNegativeFinite(linearDamping, nameof(linearDamping));
            Validate.NonNegativeFinite(jumpHeight, nameof(jumpHeight));
            Validate.NonNegativeFinite(jumpLength, nameof(jumpLength));
            Validate.PositiveFinite(simulationTimeStep, nameof(simulationTimeStep));
            BodySize = bodySize;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            JumpLength = jumpLength;
            SimulationTimeStep = simulationTimeStep;
            SupportSnapDistance = 0f;
            GroundContactTolerance = 0f;
        }

        private JumpNavigationParameters(JumpNavigationParameters source, float supportSnapDistance, float groundContactTolerance)
        {
            this = source;
            SupportSnapDistance = supportSnapDistance;
            GroundContactTolerance = groundContactTolerance;
        }

        /// <summary>Copies this profile with a main-thread-captured support distance.</summary>
        public JumpNavigationParameters WithSupportSnapDistance(float distance)
        {
            return new JumpNavigationParameters(this, distance, GroundContactTolerance);
        }

        /// <summary>Copies this profile with a main-thread-captured ground contact tolerance.</summary>
        public JumpNavigationParameters WithGroundContactTolerance(float tolerance)
        {
            Validate.NonNegativeFinite(tolerance, nameof(tolerance));
            return new JumpNavigationParameters(this, SupportSnapDistance, tolerance);
        }

        public GroundJumpParameters GetJumpParameters()
        {
            return new(BodySize, Gravity, GravityScale, LinearDamping, JumpHeight, JumpLength, SimulationTimeStep, SupportSnapDistance, GroundContactTolerance);
        }
    }

}
