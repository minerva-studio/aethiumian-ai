using Aethiumian.AI.Variables;
using System;
using UnityEngine;
namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the square root of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SquareRoot : ComponentwiseUnaryArithmetic
    {
        public override State Execute()
        {
            return ExecuteComponentwise(false);
        }

        protected override float Operation(float a) => Mathf.Sqrt(a);
        protected override int Operation(int a) => throw new InvalidOperationException("SquareRoot uses floating-point dispatch.");
        protected override Vector2 Operation(Vector2 a) => new(Mathf.Sqrt(a.x), Mathf.Sqrt(a.y));
        protected override Vector3 Operation(Vector3 a) => new(Mathf.Sqrt(a.x), Mathf.Sqrt(a.y), Mathf.Sqrt(a.z));
        protected override Vector4 Operation(Vector4 a) => new(Mathf.Sqrt(a.x), Mathf.Sqrt(a.y), Mathf.Sqrt(a.z), Mathf.Sqrt(a.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a) => throw new InvalidOperationException("SquareRoot uses floating-point dispatch.");
    }
}
