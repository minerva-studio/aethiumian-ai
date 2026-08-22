using System;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Arithmetic nodes, nodes that perform arithmetic operations on numbers.
    /// <br/>
    /// Must be instantly executed, cannot be yielded or wait.
    /// </summary>
    [Serializable]
    public abstract class Arithmetic : TreeNode
    {
        /// <summary>Fails numeric execution when an active input or output lane is NaN.</summary>
        public bool failOnNaN = false;

        /// <summary>Checks a variable's represented value for NaN when the policy is enabled.</summary>
        protected bool HasNaN([Readable] VariableFieldBase value)
        {
            return failOnNaN && value != null && value.ContainsNaN();
        }

        /// <summary>Checks a variable's active component-wise lanes for NaN when enabled.</summary>
        protected bool HasNaN([Readable] VariableFieldBase value, int componentCount)
        {
            return failOnNaN && value != null && value.Type.ComponentCount() != 0
                && value.ContainsComponentwiseNaN(componentCount);
        }

        /// <summary>Checks a computed three-lane input value for NaN when the policy is enabled.</summary>
        protected bool HasNaN(Vector3 value)
        {
            return failOnNaN && (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z));
        }

        public override void Initialize()
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// <br/>
        /// Cannot override
        /// <br/>
        /// It is very unlikely for this method to be called
        /// </summary>
        protected sealed override void OnStop()
        {
        }
    }
}
