using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Physical inputs used to solve and collision-validate one grounded jump.
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
        public float SupportSnapDistance { get; }
        public float GroundContactTolerance { get; }

        /// <summary>
        /// Captures all physical values that affect support resolution and trajectory validation.
        /// </summary>
        public GroundJumpParameters(Vector2 bodySize, Vector2 gravity, float gravityScale, float linearDamping, float jumpHeight, float jumpLength, float simulationTimeStep, float supportSnapDistance, float groundContactTolerance)
        {
            BodySize = bodySize;
            Gravity = gravity;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            JumpHeight = jumpHeight;
            JumpLength = jumpLength;
            SimulationTimeStep = simulationTimeStep;
            SupportSnapDistance = supportSnapDistance;
            GroundContactTolerance = groundContactTolerance;
        }
    }
}
