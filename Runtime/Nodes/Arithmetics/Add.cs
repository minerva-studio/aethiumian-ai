using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Add : ComponentwiseArithmetic
    {
        protected override float Operation(float a, float b) => a + b;
        protected override Vector2 Operation(Vector2 a, Vector2 b) => a + b;
        protected override Vector3 Operation(Vector3 a, Vector3 b) => a + b;
        protected override Vector4 Operation(Vector4 a, Vector4 b) => a + b;
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a, ComponentwiseInt4 b, int componentCount) => a + b;
    }
}
