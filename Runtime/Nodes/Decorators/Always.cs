using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [NodeTip("Always return a fixed value regardless the return value of its child")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Always : Decorator
    {
        [Readable]
        public VariableField<bool> returnValue = new();

        /// <summary>
        /// Returns the configured result regardless of the child's result.
        /// </summary>
        public sealed override State ReceiveReturnFromChild(bool @return)
        {
            return StateOf(returnValue);
        }

        /// <summary>
        /// Returns the configured result when no child is assigned.
        /// </summary>
        protected override State ExecuteWithoutChild()
        {
            return StateOf(returnValue);
        }
    }
}
