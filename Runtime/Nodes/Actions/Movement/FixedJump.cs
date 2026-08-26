using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Performs one fixed ballistic jump toward a target position.
    /// </summary>
    /// <remarks>
    /// Thanks to https://github.com/DuncanZh
    /// </remarks>
    [NodeTip("Perform one fixed ballistic jump toward a target position")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class FixedJump : Action
    {
        [Numeric]
        [Readable]
        public VariableField jumpHeight = new(30f);
        [Readable]
        public VariableField<float> speed = 10f;
        [Readable]
        public VariableField<float> speedModifier = 1f;
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject)]
        [Readable]
        public VariableField target;

        private IMovementSource movementSource;
        private Rigidbody2D rb;
        private JumpTrajectorySolution trajectory;
        private BallisticJumpExecutor executor;
        private MovementBackend.Mode backend;
        private float elapsed;

        /// <summary>Resolves the jump dependencies and trajectory without writing physics state.</summary>
        public override void Start()
        {
            ClearExecution();
            elapsed = 0f;

            if (target.IsNull)
            {
                End(false);
                return;
            }

            movementSource = Script as IMovementSource;
            if (movementSource == null)
            {
                Exception(new InvalidOperationException(
                    $"{nameof(FixedJump)} requires its control target to implement {nameof(IMovementSource)}."));
                return;
            }

            if (!gameObject.TryGetComponent(out rb) || !gameObject.TryGetComponent<Collider2D>(out _))
            {
                Exception(new InvalidOperationException(
                    $"{nameof(FixedJump)} requires Rigidbody2D and Collider2D on its AI GameObject."));
                return;
            }

            float maximumHorizontalSpeed = speed * speedModifier;
            if (!IsFinite(maximumHorizontalSpeed) || maximumHorizontalSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "FixedJump horizontal speed must be finite and non-negative.");
            }

            JumpTrajectoryInput input = new(rb.position, target.PositionValue, Physics2D.gravity,
                rb.gravityScale, rb.linearDamping, jumpHeight.NumericValue, Time.fixedDeltaTime);
            if (!JumpTrajectory.TrySolve(input, out trajectory))
            {
                End(false);
                return;
            }

            if (Mathf.Abs(trajectory.InitialVelocity.x) > maximumHorizontalSpeed + 0.000001f)
            {
                End(false);
                return;
            }

            backend = MovementBackend.Current;
            if (backend == MovementBackend.Mode.New)
            {
                executor = new BallisticJumpExecutor(rb, trajectory);
            }
        }

        /// <summary>Advances the selected jump implementation when intentional movement is permitted.</summary>
        public override void FixedUpdate()
        {
            if (!movementSource.CanMove)
            {
                return;
            }

            bool completed = backend == MovementBackend.Mode.New
                ? executor.Tick(Time.fixedDeltaTime)
                : TickLegacy(Time.fixedDeltaTime);
            if (completed)
            {
                ClearExecution();
                End(true);
            }
        }

        /// <summary>Releases runtime-only execution state when the action is stopped or completed.</summary>
        public override void OnDestroy()
        {
            ClearExecution();
        }

        /// <summary>Advances the legacy velocity-driven jump implementation.</summary>
        private bool TickLegacy(float fixedDeltaTime)
        {
            elapsed = Mathf.Min(trajectory.FlightDuration, elapsed + fixedDeltaTime);
            rb.linearVelocity = trajectory.GetVelocity(elapsed);
            if (elapsed < trajectory.FlightDuration)
            {
                return false;
            }

            rb.linearVelocityX = 0f;
            return true;
        }

        /// <summary>Clears execution references owned by the current action run.</summary>
        private void ClearExecution()
        {
            executor = null;
            trajectory = null;
            movementSource = null;
            rb = null;
        }

        /// <summary>Returns whether a scalar is finite.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
