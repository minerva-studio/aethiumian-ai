using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// reverse the return value of the child node
    /// <br/>
    /// return the inverse the return value
    /// </summary>
    [Serializable]
    [NodeTip("An inverter of the return value of its child node")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Inverter : Flow
    {
        public NodeReference node;

        public sealed override State Execute()
        {
            if (behaviourTree.GetNode(node) != null)
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
        }
    }
}
