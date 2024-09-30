using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Calling Physics2D raycast, returning result of hitting or not")]
    public class Raycast2D : Call
    {
        public VariableField<Vector2> center;
        public VariableField<Vector2> direction;
        public VariableField<float> distance = -1f;
        public VariableField<LayerMask> layerMask;


        [Constraint(VariableType.Generic)]
        public VariableReference result;


        public override State Execute()
        {
            var hit = Physics2D.Raycast(center, direction, distance, (LayerMask)layerMask);
            this.result.SetValue(hit);
            return StateOf(hit.collider != null);
        }
    }
}