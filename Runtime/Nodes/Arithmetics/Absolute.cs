using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the absolute value of a numeric value or vector.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Absolute : ComponentwiseUnaryArithmetic
    {
        protected override float Operation(float a) => Mathf.Abs(a);
        protected override int Operation(int a) => a < 0 ? unchecked(-a) : a;
        protected override Vector2 Operation(Vector2 a) => new(Mathf.Abs(a.x), Mathf.Abs(a.y));
        protected override Vector3 Operation(Vector3 a) => new(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));
        protected override Vector4 Operation(Vector4 a) => new(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z), Mathf.Abs(a.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a) => ComponentwiseInt4.Abs(a);
    }
}
