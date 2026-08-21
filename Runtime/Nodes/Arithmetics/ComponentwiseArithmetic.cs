using System;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Shared target-shape and numeric-mode behavior for componentwise binary operations.</summary>
    [Serializable]
    public abstract class ComponentwiseArithmetic : Arithmetic
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

        /// <summary>
        /// Determines whether the operands and destination are valid for the current
        /// componentwise arithmetic mode.
        /// </summary>
        protected bool IsComponentwiseBinaryOperationValid(VariableField a, VariableField b, VariableReference result)
        {
            if (!a.Type.IsComponentwiseType() || !b.Type.IsComponentwiseType() || !result.Type.IsComponentwiseType())
            {
                return false;
            }

            if (result.Type == VariableType.Int || result.Type == VariableType.Float)
            {
                return true;
            }
            else
            {
                return EffectiveMode != ArithmeticMode.Int;
            }
        }
    }
}
