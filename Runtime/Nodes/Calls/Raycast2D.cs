using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Performs a 2D physics raycast and returns whether it hit.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class Raycast2D : Call
    {
        [Readable] public VariableField<Vector2> center;
        [Readable] public VariableField<Vector2> direction;
        [Readable] public VariableField<float> distance = -1f;
        public VariableField<LayerMask> layerMask;


        [Constraint(VariableType.Generic)]
        [Writable] public VariableReference result;


        public override State Execute()
        {
            var hit = Physics2D.Raycast(center, direction, distance, (LayerMask)layerMask);
            this.result.SetValue(hit);
            return StateOf(hit.collider != null);
        }
    }
}
