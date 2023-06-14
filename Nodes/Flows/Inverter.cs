using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// reverse the return value of the child node
    /// <br/>
    /// return the inverse the return value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    [NodeTip("An inverter of the return value of its child node")]
    public sealed class Inverter : Flow
    {
        public NodeReference node;

        public sealed override State Execute()
        {
            if (node.HasReference)
            {
                return SetNextExecute(node);
            }
            else
            {
                return State.Failed;
            }
        }

        public sealed override State ReceiveReturnFromChild(bool @return)
        {
            return StateOf(!@return);
        }

        public sealed override void Initialize()
        {
            node = behaviourTree.References[node.UUID].ToReference();
        }
    }
}