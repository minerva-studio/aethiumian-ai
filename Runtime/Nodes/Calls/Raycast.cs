using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Performs a 3D physics raycast and returns whether it hit.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class Raycast : Call
    {
        [Readable] public VariableField<Vector3> center;
        [Readable] public VariableField<Vector3> direction;
        [Readable] public VariableField<float> distance = -1f;
        public VariableField<LayerMask> layerMask;


        [Constraint(VariableType.Generic)]
        [Writable] public VariableReference result;


        public override State Execute()
        {
            Physics.Raycast(new Ray(center, direction), out RaycastHit hit, distance, (LayerMask)layerMask);
            result.SetValue(hit);
            return StateOf(hit.collider != null);
        }
    }
}
