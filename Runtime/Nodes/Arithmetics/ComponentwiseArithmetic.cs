using System;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Shared target-shape and numeric-mode behavior for componentwise binary operations.</summary>
    [Serializable]
    public abstract class ComponentwiseArithmetic : ComponentwiseBinaryArithmetic
    {
        /// <summary>Node-level mode. Default inherits the containing tree's mode.</summary>
        public ArithmeticMode operationMode = ArithmeticMode.Default;

        /// <summary>Resolves the node mode, with tree Default treated as Float.</summary>
        protected ArithmeticMode EffectiveMode
        {
            get
            {
                if (operationMode != ArithmeticMode.Default)
                {
                    return operationMode;
                }

                if (behaviourTree != null && behaviourTree.Prototype != null &&
                    behaviourTree.Prototype.arithmeticMode != ArithmeticMode.Default)
                {
                    return behaviourTree.Prototype.arithmeticMode;
                }

                return ArithmeticMode.Float;
            }
        }

        /// <summary>Tries to resolve the explicit arithmetic mode independently from destination shape.</summary>
        protected bool TryResolveOperationMode(
            VariableField a,
            VariableField b,
            VariableReference result,
            out ArithmeticMode mode)
        {
            int componentCount = result.Type.ComponentCount();
            if (!SupportsComponentwiseOperands(a, b) || componentCount == 0)
            {
                mode = default;
                return false;
            }

            mode = EffectiveMode == ArithmeticMode.Int ? ArithmeticMode.Int : ArithmeticMode.Float;
            return true;
        }
    }
}
