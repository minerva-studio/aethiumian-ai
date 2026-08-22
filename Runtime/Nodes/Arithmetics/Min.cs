using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Calculates the component-wise minimum of two numeric values or vectors.</summary>
    [Serializable]
    [NodeTip("Calculates the component-wise minimum of two numeric values or vectors.")]
    public sealed class Min : ComponentwiseBinaryArithmetic
    {
        protected override bool TryResolveOperationDomain(out bool useIntegerDomain)
        {
            useIntegerDomain = HasIntegerComponents(a) && HasIntegerComponents(b);
            return true;
        }

        protected override float Operation(float a, float b) => Mathf.Min(a, b);
        protected override Vector2 Operation(Vector2 a, Vector2 b) => new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        protected override Vector3 Operation(Vector3 a, Vector3 b) => new(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        protected override Vector4 Operation(Vector4 a, Vector4 b) => new(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z), Mathf.Min(a.w, b.w));
        protected override ComponentwiseInt4 Operation(ComponentwiseInt4 a, ComponentwiseInt4 b, int componentCount)
            => ComponentwiseInt4.Min(a, b);
    }
}
