using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Is given target in vision")]
    public class IsInVision : Determine
    {
        public VariableReference<UnityEngine.Object> target;
        public VariableField<Vector2> offset;
        public VariableField<float> maxDistance = -1;
        public LayerMask blockingLayers;

        public override Exception IsValidNode()
        {
            if (!target.HasValue || target.IsVector || !target.CanBeGameObject)
            {
                return InvalidNodeException.VariableIsRequired(nameof(target));
            }
            return null;
        }

        public override bool GetValue()
        {
            Vector2 dst = target.TransformValue.position;
            Vector2 position = (Vector2)transform.position + offset;
            Vector2 disp = dst - position;
            float magnitude = maxDistance > 0 ? maxDistance : disp.magnitude;
            RaycastHit2D hit = Physics2D.Raycast(position, disp, magnitude, blockingLayers);
            if (maxDistance > 0)
            {
                return hit.collider == null && disp.magnitude < maxDistance;
            }
            return hit.collider == null;
        }
    }
}