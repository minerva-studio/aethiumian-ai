using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Divides one numeric value or vector by another.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Divide : ComponentwiseArithmetic
    {
        protected override float Operation(float a, float b) => a / b;
        protected override Vector2 Operation(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);
        protected override Vector3 Operation(Vector3 a, Vector3 b) => new(a.x / b.x, a.y / b.y, a.z / b.z);
        protected override Vector4 Operation(Vector4 a, Vector4 b) => new(a.x / b.x, a.y / b.y, a.z / b.z, a.w / b.w);
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a, ComponentwiseInt4 b, int componentCount)
            => ComponentwiseInt4.Divide(a, b, componentCount);
    }
}
