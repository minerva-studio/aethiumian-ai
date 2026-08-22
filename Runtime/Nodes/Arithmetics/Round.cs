using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Applies round to a scalar or every component of a vector.
    /// </summary>
    [Serializable]
    [NodeTip("Applies round to a scalar or every component of a vector.")]
    public sealed class Round : ComponentwiseUnaryArithmetic
    {
        protected override float Operation(float a) => Mathf.Round(a);
        protected override int Operation(int a) => a; // Round of an integer is the integer itself, so we return it unchanged.
        protected override Vector2 Operation(Vector2 a) => new Vector2(Mathf.Round(a.x), Mathf.Round(a.y));
        protected override Vector3 Operation(Vector3 a) => new Vector3(Mathf.Round(a.x), Mathf.Round(a.y), Mathf.Round(a.z));
        protected override Vector4 Operation(Vector4 a) => new Vector4(Mathf.Round(a.x), Mathf.Round(a.y), Mathf.Round(a.z), Mathf.Round(a.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a) => a; // Round of an integer is the integer itself, so we return it unchanged.
    }
}
