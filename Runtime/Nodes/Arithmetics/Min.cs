using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Calculates the component-wise minimum of two numeric values or vectors.
    /// </summary>
    [Serializable]
    [NodeTip("Calculates the component-wise minimum of two numeric values or vectors.")]
    public sealed class Min : ComponentwiseBinaryArithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableField a;

        [NumericOrVector]
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference result;

        /// <summary>Executes a target-shape component-wise minimum without boxing input values.</summary>
        public override State Execute()
        {
            try
            {
                int componentCount = result.Type.ComponentCount();
                if (!SupportsComponentwiseOperands(a, b) || componentCount == 0)
                {
                    return State.Failed;
                }
                if (HasNaNOperands(a, b, componentCount))
                {
                    return State.Failed;
                }

                if (HasIntegerComponents(a) && HasIntegerComponents(b))
                {
                    result.SetComponentwiseValue(ComponentwiseInt4.Min(a.IntComponentwiseValue, b.IntComponentwiseValue));
                    return State.Success;
                }

                Vector4 left = a.ComponentwiseValue;
                Vector4 right = b.ComponentwiseValue;
                Vector4 value = new Vector4(
                    Mathf.Min(left.x, right.x),
                    Mathf.Min(left.y, right.y),
                    Mathf.Min(left.z, right.z),
                    Mathf.Min(left.w, right.w));
                return result.SetComponentwiseValue(value, failOnNaN)
                    ? State.Success
                    : State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
