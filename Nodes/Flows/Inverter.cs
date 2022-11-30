using System;

namespace Amlos.AI
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

        public sealed override void Execute()
        {
            if (node.HasReference)
                SetNextExecute(node);
            else End(false);
        }

        public sealed override void Initialize()
        {
            node = behaviourTree.References[node.uuid].ToReference();
        }
    }
}