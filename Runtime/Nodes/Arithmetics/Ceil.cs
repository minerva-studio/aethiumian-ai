using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Applies ceiling to a scalar or every component of a vector.
    /// </summary>
    [Serializable]
    [NodeTip("Applies ceiling to a scalar or every component of a vector.")]
    public sealed class Ceil : ComponentwiseUnaryArithmetic
    {
        protected override float Operation(float a) => Mathf.Ceil(a);
        protected override int Operation(int a) => a; // Ceiling of an integer is the integer itself, so we return it unchanged.
        protected override Vector2 Operation(Vector2 a) => new Vector2(Mathf.Ceil(a.x), Mathf.Ceil(a.y));
        protected override Vector3 Operation(Vector3 a) => new Vector3(Mathf.Ceil(a.x), Mathf.Ceil(a.y), Mathf.Ceil(a.z));
        protected override Vector4 Operation(Vector4 a) => new Vector4(Mathf.Ceil(a.x), Mathf.Ceil(a.y), Mathf.Ceil(a.z), Mathf.Ceil(a.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a) => a; // Ceiling of an integer is the integer itself, so we return it unchanged.
    }
}
