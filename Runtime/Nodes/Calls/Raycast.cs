using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Calling Physics raycast, returning result of hitting or not")]
    public class Raycast : Call
    {
        public VariableField<Vector3> center;
        public VariableField<Vector3> direction;
        public VariableField<float> distance = -1f;
        public VariableField<LayerMask> layerMask;


        [Constraint(VariableType.Generic)]
        public VariableReference result;


        public override State Execute()
        {
            Physics.Raycast(new Ray(center, direction), out RaycastHit hit, distance, (LayerMask)layerMask);
            result.SetValue(hit);
            return StateOf(hit.collider != null);
        }
    }
}