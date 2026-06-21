using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class AddForce : Call
    {
        public VariableField<Vector2> force;

        Rigidbody2D rb;

        public override State Execute()
        {
            if (!rb) return State.Failed;

            rb.AddForce(force);
            return State.Success;
        }

        public override void Initialize()
        {
            rb = gameObject.GetComponent<Rigidbody2D>();
        }
    }
}