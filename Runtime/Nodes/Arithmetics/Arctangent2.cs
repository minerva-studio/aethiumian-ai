using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the angle from two numeric inputs.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arctangent2 : ComponentwiseBinaryArithmetic
    {
        protected override float Operation(float y, float x) => Mathf.Atan2(y, x);
        protected override Vector2 Operation(Vector2 y, Vector2 x) => new(
            Mathf.Atan2(y.x, x.x),
            Mathf.Atan2(y.y, x.y));
        protected override Vector3 Operation(Vector3 y, Vector3 x) => new(
            Mathf.Atan2(y.x, x.x),
            Mathf.Atan2(y.y, x.y),
            Mathf.Atan2(y.z, x.z));
        protected override Vector4 Operation(Vector4 y, Vector4 x) => new(
            Mathf.Atan2(y.x, x.x),
            Mathf.Atan2(y.y, x.y),
            Mathf.Atan2(y.z, x.z),
            Mathf.Atan2(y.w, x.w));

        protected override bool ValidateInput(Vector4 y, Vector4 x, int componentCount)
        {
            return !IsZeroPair(y.x, x.x)
                && (componentCount < 2 || !IsZeroPair(y.y, x.y))
                && (componentCount < 3 || !IsZeroPair(y.z, x.z))
                && (componentCount < 4 || !IsZeroPair(y.w, x.w));
        }

        private static bool IsZeroPair(float y, float x)
        {
            return x == 0f && y == 0f;
        }
    }
}
