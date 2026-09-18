using Aethiumian.AI.Navigation;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Applies a bounded continuous force through the authored force mode.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Sprint : Aethiumian.AI.Nodes.Action
    {
        public float duration;
        public Vector2 force;

        /// <summary>Selects either the current bounded force action or the historical launch-and-repeat force behavior.</summary>
        public enum ForceApplication { Timed = 0, Legacy = 1 }

        /// <summary>Authored per node; defaults to the current timed executor behavior.</summary>
        public ForceApplication forceApplication;
        private ForceApplication activeForceApplication;
        private IMovementSource movementSource;
        private TimedForceExecutor executor;
        private float current;

        /// <summary>Caches the authored force mode and prepares its execution state.</summary>
        public override void Start()
        {
            activeForceApplication = forceApplication;
            current = 0f;
            movementSource = Script as IMovementSource
                ?? throw new InvalidOperationException($"{nameof(Sprint)} requires its ControlTarget to implement {nameof(IMovementSource)}.");
            if (activeForceApplication == ForceApplication.Legacy)
            {
                var legacyBody = gameObject.GetComponent<Rigidbody2D>();
                legacyBody.AddForce(force);
                ReportMovementState(MovementState.Sprinting);
                return;
            }

            var body = gameObject.GetComponent<Rigidbody2D>();
            if (!body)
            {
                throw new InvalidOperationException($"{nameof(Sprint)} requires a {nameof(Rigidbody2D)} on the AI host GameObject.");
            }

            executor = new TimedForceExecutor(body, force, ForceMode2D.Force, duration);
        }

        /// <summary>Advances the selected maneuver from the behaviour tree's fixed-update path.</summary>
        public override void FixedUpdate()
        {
            if (activeForceApplication == ForceApplication.Legacy)
            {
                LegacyFixedUpdate();
                return;
            }

            if (!movementSource.CanMove)
            {
                ReportMovementState(MovementState.Idle);
                Fail();
                return;
            }

            ExecutionResult result = executor.Tick(Time.fixedDeltaTime);
            if (result.Status == ExecutionStatus.Running)
            {
                ReportMovementState(MovementState.Sprinting);
                return;
            }

            ReportMovementState(MovementState.Idle);
            if (result.Status == ExecutionStatus.Completed)
            {
                Success();
            }
        }

        /// <summary>Releases runtime-only references owned by the current execution.</summary>
        public override void OnDestroy()
        {
            ReportMovementState(MovementState.Idle);
            movementSource = null;
            executor?.Dispose();
            executor = null;
        }

        /// <summary>Advances the original Sprint implementation without changing its behavior.</summary>
        private void LegacyFixedUpdate()
        {
            var body = gameObject.GetComponent<Rigidbody2D>();
            body.AddForce(force);
            ReportMovementState(MovementState.Sprinting);

            current += Time.fixedDeltaTime;
            if (current > duration)
            {
                ReportMovementState(MovementState.Idle);
                End(true);
            }
        }

        private void ReportMovementState(MovementState state)
        {
            if (state == MovementState.Unspecified || movementSource == null) return;
            try
            {
                movementSource.SetMovementState(new MovementStateInfo(state));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, gameObject);
            }
        }
    }
}
