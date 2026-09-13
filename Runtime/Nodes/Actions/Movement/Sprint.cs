using Aethiumian.AI.Navigation;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Applies a bounded continuous force through the selected movement backend.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Library-of-Meialia-AI")]
    public class Sprint : Aethiumian.AI.Nodes.Action
    {
        public float duration;
        public Vector2 force;

        private MovementBackend.Mode backend;
        private IMovementSource movementSource;
        private TimedForceExecutor executor;
        private float current;

        /// <summary>Caches the selected backend and prepares its execution state.</summary>
        public override void Start()
        {
            backend = MovementBackend.Current;
            if (backend == MovementBackend.Mode.Legacy)
            {
                var legacyBody = gameObject.GetComponent<Rigidbody2D>();
                legacyBody.AddForce(force);
                return;
            }

            movementSource = Script as IMovementSource
                ?? throw new InvalidOperationException($"{nameof(Sprint)} requires its ControlTarget to implement {nameof(IMovementSource)}.");
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
            if (backend == MovementBackend.Mode.Legacy)
            {
                LegacyFixedUpdate();
                return;
            }

            if (!movementSource.CanMove)
            {
                Fail();
                return;
            }

            if (executor.Tick(Time.fixedDeltaTime).Status == ExecutionStatus.Completed)
            {
                Success();
            }
        }

        /// <summary>Releases runtime-only references owned by the current execution.</summary>
        public override void OnDestroy()
        {
            movementSource = null;
            executor?.Dispose();
            executor = null;
        }

        /// <summary>Advances the original Sprint implementation without changing its behavior.</summary>
        private void LegacyFixedUpdate()
        {
            var body = gameObject.GetComponent<Rigidbody2D>();
            body.AddForce(force);

            current += Time.fixedDeltaTime;
            if (current > duration)
            {
                End(true);
            }
        }
    }
}
