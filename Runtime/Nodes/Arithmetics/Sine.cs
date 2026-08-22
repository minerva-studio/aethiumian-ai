using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the sine of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Sine : ComponentwiseUnaryArithmetic
    {
        public override State Execute() => ExecuteComponentwise(false);

        protected override float Operation(float value) => Mathf.Sin(value);
        protected override int Operation(int value) => throw new InvalidOperationException("Sine uses floating-point dispatch.");
        protected override Vector2 Operation(Vector2 value) => new(Mathf.Sin(value.x), Mathf.Sin(value.y));
        protected override Vector3 Operation(Vector3 value) => new(Mathf.Sin(value.x), Mathf.Sin(value.y), Mathf.Sin(value.z));
        protected override Vector4 Operation(Vector4 value) => new(Mathf.Sin(value.x), Mathf.Sin(value.y), Mathf.Sin(value.z), Mathf.Sin(value.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 value) => throw new InvalidOperationException("Sine uses floating-point dispatch.");
    }
}
