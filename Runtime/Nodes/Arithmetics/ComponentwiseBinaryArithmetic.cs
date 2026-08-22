using System;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Shared operand and shape helpers for binary operations over scalar and vector values.</summary>
    [Serializable]
    public abstract class ComponentwiseBinaryArithmetic : Arithmetic
    {
        /// <summary>Checks both operands within the destination shape.</summary>
        protected bool HasNaNOperands([Readable] VariableField a, [Readable] VariableField b, int componentCount)
        {
            return HasNaN(a, componentCount) || HasNaN(b, componentCount);
        }

        /// <summary>Determines whether both operands can participate in component-wise math.</summary>
        protected static bool SupportsComponentwiseOperands([Readable] VariableField a, [Readable] VariableField b)
        {
            return a.Type.ComponentCount() != 0 && b.Type.ComponentCount() != 0;
        }

        /// <summary>Determines whether a value has a naturally discrete component representation.</summary>
        protected static bool HasIntegerComponents(VariableField value)
        {
            return value.Type == VariableType.Int || value.Type == VariableType.Bool;
        }
    }
}
