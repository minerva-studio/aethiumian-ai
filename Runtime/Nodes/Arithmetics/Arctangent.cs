using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the arctangent of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arctangent : ComponentwiseUnaryArithmetic
    {
        public override State Execute() => ExecuteComponentwise(false);

        protected override float Operation(float value) => Mathf.Atan(value);
        protected override int Operation(int value) => throw new InvalidOperationException("Arctangent uses floating-point dispatch.");
        protected override Vector2 Operation(Vector2 value) => new(Mathf.Atan(value.x), Mathf.Atan(value.y));
        protected override Vector3 Operation(Vector3 value) => new(Mathf.Atan(value.x), Mathf.Atan(value.y), Mathf.Atan(value.z));
        protected override Vector4 Operation(Vector4 value) => new(Mathf.Atan(value.x), Mathf.Atan(value.y), Mathf.Atan(value.z), Mathf.Atan(value.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 value) => throw new InvalidOperationException("Arctangent uses floating-point dispatch.");
    }
}
