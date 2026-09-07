using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Is given target in vision")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class IsInVision : Determine
    {
        [Readable]
        public VariableReference<UnityEngine.Object> target;
        [Readable]
        public VariableField<Vector2> offset;
        [Readable]
        public VariableField<float> maxDistance = -1;

        /// <summary>Chooses the distance geometry used by the range part of the visibility query.</summary>
        public DistanceTo.Measurement measurement = DistanceTo.Measurement.ColliderSurface;

        public LayerMask blockingLayers;

        private Collider2D collider;
        private Collider2D targetCollider;

        public Collider2D Collider => collider ? collider : collider = gameObject.GetComponent<Collider2D>();
        public Collider2D TargetCollider => targetCollider ? targetCollider : targetCollider = target.GameObjectValue.GetComponent<Collider2D>();

        public override Exception IsValidNode()
        {
            if (!target.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(target), this);
            }
            return null;
        }

        public override bool GetValue()
        {
            if (target.IsNull)
            {
                return false;
            }

            Vector2 dst = target.PositionValue;
            Vector2 position = (Vector2)transform.position + offset;
            Collider2D selfCollider = Collider;
            Collider2D targetCollider = TargetCollider;
            return TargetVisibilityQuery.IsVisible(
                position,
                dst,
                selfCollider,
                targetCollider,
                blockingLayers,
                maxDistance,
                measurement);
        }

    }
}
