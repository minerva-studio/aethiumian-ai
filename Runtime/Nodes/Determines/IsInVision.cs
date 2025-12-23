using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Is given target in vision")]
    public class IsInVision : Determine
    {
        [Readable]
        public VariableReference<UnityEngine.Object> target;
        [Readable]
        public VariableField<Vector2> offset;
        [Readable]
        public VariableField<float> maxDistance = -1;

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
            Vector2 disp = dst - position;
            Collider2D selfCollider = Collider;
            Collider2D targetCollider = TargetCollider;

            float maxDistance = this.maxDistance;
            float rayCastMagnitude = maxDistance > 0 ? maxDistance : disp.magnitude;
            float realDistance = GetDistance();

            RaycastHit2D hit = Physics2D.Raycast(position, disp, rayCastMagnitude, blockingLayers);
            if (hit.collider)
            {
                float wallDistance = hit.distance;
                if (realDistance < wallDistance) return true;
                //Debug.Log("Hit something like " + hit.collider);
                return false;
            }
            if (maxDistance <= 0)
            {
                return true;
            }
            return realDistance < maxDistance;


            float GetDistance()
            {
                if (selfCollider && targetCollider)
                {
                    ColliderDistance2D colliderDistance2D = targetCollider.Distance(selfCollider);
                    float distance = colliderDistance2D.distance;
                    if (colliderDistance2D.isValid)
                        return distance;
                }
                if (!selfCollider && !targetCollider)
                {
                    return disp.magnitude;
                }
                if (selfCollider)
                {
                    return (selfCollider.ClosestPoint(dst) - dst).magnitude;
                }
                return (targetCollider.ClosestPoint(position) - position).magnitude;
            }
        }

    }
}