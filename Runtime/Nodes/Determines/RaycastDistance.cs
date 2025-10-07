using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    public sealed class RaycastDistance : ComparableDetermine<float>
    {
        public enum PhysicsMode
        {
            Physics3D,
            Physics2D,
        }

        public PhysicsMode physicsMode = PhysicsMode.Physics2D;
        [Readable]
        [Constraint(VariableType.UnityObject, VariableType.Vector2, VariableType.Vector3)]
        public VariableField center;
        [Readable]
        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        public VariableField direction;
        [Readable]
        public VariableField<float> distance = -1f;
        [Readable]
        public VariableField<LayerMask> layerMask;




        public override float GetValue()
        {

            float maxDistnace = this.distance;

            switch (physicsMode)
            {
                case PhysicsMode.Physics2D:
                    {
                        Vector2 center = this.center.PositionValue;
                        Vector2 direction = this.direction.Vector2Value;
                        RaycastHit2D hit;

                        if (maxDistnace > 0) hit = Physics2D.Raycast(center, direction, maxDistnace, (LayerMask)layerMask);
                        else hit = Physics2D.Raycast(center, direction, (LayerMask)layerMask);

                        return hit ? hit.distance : float.MaxValue;
                    }
                case PhysicsMode.Physics3D:
                default:
                    {
                        Vector3 center = this.center.PositionValue;
                        Vector3 direction = this.direction.Vector3Value;
                        Ray ray = new(center, direction);
                        RaycastHit hit;

                        if (maxDistnace > 0) Physics.Raycast(ray, out hit, distance, (LayerMask)layerMask);
                        else Physics.Raycast(ray, out hit, (LayerMask)layerMask);

                        return hit.collider ? hit.distance : float.MaxValue;
                    }
            }

        }
    }
}