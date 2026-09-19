using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Applies one force or repeats it over a bounded sprint window.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Sprint : Aethiumian.AI.Nodes.Action
    {
        /// <summary>
        /// Duration of the reported Sprinting movement-state window, captured when the action starts.
        /// </summary>
        [Readable]
        public VariableField<float> duration = 0f;

        /// <summary>
        /// Authored or bound force vector submitted with <see cref="ForceMode2D.Force"/>.
        /// </summary>
        [Readable]
        public VariableField<Vector2> force = Vector2.zero;

        /// <summary>
        /// Selects whether the configured force is applied once or on every active fixed tick.
        /// </summary>
        public enum ForceApplication
        {
            Once = 0,
            Repeated = 1
        }

        /// <summary>
        /// Chooses the force cadence; defaults to one application to preserve single-call force behavior.
        /// </summary>
        public ForceApplication forceApplication = ForceApplication.Once;

        private IMovementSource movementSource;
        private Rigidbody2D body;
        private TimedForceExecutor executor;
        private ForceApplication activeForceApplication;
        private Vector2 activeForce;
        private float activeDuration;
        private float elapsed;
        private MovementState reportedMovementState = MovementState.Unspecified;






        /// <summary>
        /// Validates the host and movement source, then starts a one-shot application when selected.
        /// </summary>
        public override void Start()
        {
            activeForceApplication = forceApplication;
            elapsed = 0f;
            reportedMovementState = MovementState.Unspecified;
            movementSource = Script as IMovementSource ?? throw new InvalidOperationException($"{nameof(Sprint)} requires its ControlTarget to implement {nameof(IMovementSource)}.");

            body = gameObject.GetComponent<Rigidbody2D>();
            if (!body)
            {
                throw new InvalidOperationException($"{nameof(Sprint)} requires a {nameof(Rigidbody2D)} on the AI host GameObject.");
            }

            activeForce = force;
            activeDuration = duration;
            Validate.Finite(activeForce, nameof(force));
            Validate.NonNegativeFinite(activeDuration, nameof(duration));

            switch (activeForceApplication)
            {
                case ForceApplication.Once:
                    if (!movementSource.CanMove)
                    {
                        Finish(false);
                        return;
                    }

                    body.AddForce(activeForce, ForceMode2D.Force);
                    if (activeDuration > 0f)
                    {
                        ReportMovementState(MovementState.Sprinting);
                    }
                    else
                    {
                        ReportMovementState(MovementState.Idle);
                        Success();
                    }
                    break;

                case ForceApplication.Repeated:
                    executor = new TimedForceExecutor(body, activeForce, ForceMode2D.Force, activeDuration);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(forceApplication), forceApplication, "Unknown force application strategy.");
            }
        }

        /// <summary>
        /// Advances the selected force strategy and reports its active movement window.
        /// </summary>
        public override void FixedUpdate()
        {
            if (IsComplete) return;

            if (!movementSource.CanMove)
            {
                Finish(false);
                return;
            }

            if (activeForceApplication == ForceApplication.Once)
            {
                elapsed += Time.fixedDeltaTime;
                if (elapsed >= activeDuration)
                {
                    Finish(true);
                }
                return;
            }

            if (activeDuration > 0f)
            {
                ReportMovementState(MovementState.Sprinting);
            }

            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            if (result.Status == ExecutionStatus.Running) return;

            Finish(result.Status == ExecutionStatus.Completed);
        }

        /// <summary>
        /// Returns the movement source to idle and releases the active timed executor.
        /// </summary>
        public override void OnDestroy()
        {
            ReportMovementState(MovementState.Idle);
            movementSource = null;
            body = null;
            executor?.Dispose();
            executor = null;
        }

        private void Finish(bool success)
        {
            ReportMovementState(MovementState.Idle);
            if (success)
            {
                Success();
            }
            else
            {
                Fail();
            }
        }

        private void ReportMovementState(MovementState state)
        {
            if (state == MovementState.Unspecified || movementSource == null) return;
            if (reportedMovementState == state) return;
            try
            {
                movementSource.SetMovementState(new MovementStateInfo(state));
                reportedMovementState = state;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, gameObject);
            }
        }
    }
}
