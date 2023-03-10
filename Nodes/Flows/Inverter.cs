using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// reverse the return value of the child node
    /// <br></br>
    /// return the inverse the return value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public sealed class Inverter : Flow
    {
        public NodeReference node;

        public sealed override State Execute()
        {
            if (node.HasReference)
            {
                SetNextExecute(node);
                return State.NONE_RETURN;
            }
            else
            {
                return State.Failed;
            }
        }

        public sealed override void Initialize()
        {
            node = behaviourTree.References[node.UUID].ToReference();
        }
    }
}