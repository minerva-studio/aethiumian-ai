using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Physical inputs and independent support requirements used to solve and collision-validate one jump.
    /// </summary>
    public readonly struct GroundJumpParameters
    {
        public Vector2 BodySize { get; }
        public Vector2 Gravity { get; }
        public float GravityScale { get; }
        public float LinearDamping { get; }
        public float JumpHeight { get; }
        public float JumpLength { get; }
        public float SimulationTimeStep { get; }

        /// <summary>Gets whether an unsupported launch is allowed; true uses the supplied launch anchor unchanged.</summary>
        public bool AllowAirborneLaunch { get; }

        /// <summary>Gets whether an unsupported endpoint is allowed; true uses the supplied endpoint unchanged.</summary>
        public bool AllowAirborneEndpoint { get; }

        /// <summary>
        /// Captures all authored physical values that affect the jump trajectory.
        /// Both airborne options default to false. Enabling either option keeps its
        /// supplied anchor unchanged and retains collision and jump-range validation.
        /// </summary>
        public GroundJumpParameters(Vector2 bodySize, Vector2 gravity, float gravityScale, float linearDamping, float jumpHeight, float jumpLength, float simulationTimeStep, bool allowAirborneLaunch = false, bool allowAirborneEndpoint = false)
        {
            BodySize = bodySize;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            JumpLength = jumpLength;
            SimulationTimeStep = simulationTimeStep;
            AllowAirborneLaunch = allowAirborneLaunch;
            AllowAirborneEndpoint = allowAirborneEndpoint;
        }
    }
}
