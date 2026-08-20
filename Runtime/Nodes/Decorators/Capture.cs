using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Stores the decorated child's result in a boolean variable and returns it unchanged.
    /// </summary>
    [Serializable]
    [NodeTip("Capture the result of a child node without changing it")]
    public sealed class Capture : Decorator
    {
        [Writable]
        public VariableReference<bool> result = new();

        /// <summary>
        /// Stores and forwards the child's successful or failed result.
        /// </summary>
        public override State ReceiveReturnFromChild(bool @return)
        {
            if (result?.HasReference == true)
            {
                result.SetValue(@return);
            }

            return StateOf(@return);
        }
    }
}
