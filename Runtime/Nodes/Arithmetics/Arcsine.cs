using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the arcsine of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arcsine : ComponentwiseUnaryArithmetic
    {
        public override State Execute() => ExecuteComponentwise(false);

        protected override float Operation(float value) => Mathf.Asin(value);
        protected override int Operation(int value) => throw new InvalidOperationException("Arcsine uses floating-point dispatch.");
        protected override Vector2 Operation(Vector2 value) => new(Mathf.Asin(value.x), Mathf.Asin(value.y));
        protected override Vector3 Operation(Vector3 value) => new(Mathf.Asin(value.x), Mathf.Asin(value.y), Mathf.Asin(value.z));
        protected override Vector4 Operation(Vector4 value) => new(Mathf.Asin(value.x), Mathf.Asin(value.y), Mathf.Asin(value.z), Mathf.Asin(value.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 value) => throw new InvalidOperationException("Arcsine uses floating-point dispatch.");

        protected override bool ValidateInput(Vector4 value, int componentCount)
        {
            return IsValid(value.x)
                && (componentCount < 2 || IsValid(value.y))
                && (componentCount < 3 || IsValid(value.z))
                && (componentCount < 4 || IsValid(value.w));
        }

        private static bool IsValid(float value)
        {
            return float.IsNaN(value) || (value >= -1f && value <= 1f);
        }
    }
}
