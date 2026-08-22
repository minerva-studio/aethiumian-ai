using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Applies floor to a scalar or every component of a vector.
    /// </summary>
    [Serializable]
    [NodeTip("Applies floor to a scalar or every component of a vector.")]
    public sealed class Floor : ComponentwiseUnaryArithmetic
    {
        protected override float Operation(float a) => Mathf.Floor(a);
        protected override int Operation(int a) => a; // Floor of an integer is the integer itself, so we return it unchanged.
        protected override Vector2 Operation(Vector2 a) => new Vector2(Mathf.Floor(a.x), Mathf.Floor(a.y));
        protected override Vector3 Operation(Vector3 a) => new Vector3(Mathf.Floor(a.x), Mathf.Floor(a.y), Mathf.Floor(a.z));
        protected override Vector4 Operation(Vector4 a) => new Vector4(Mathf.Floor(a.x), Mathf.Floor(a.y), Mathf.Floor(a.z), Mathf.Floor(a.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a) => a; // Floor of an integer is the integer itself, so we return it unchanged.
    }
}
