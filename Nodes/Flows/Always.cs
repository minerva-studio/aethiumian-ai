using Amlos.AI.References;
using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{

    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [NodeTip("Always return a fixed value regardless the return value of its child")]
    public sealed class Always : Flow
    {
        public NodeReference node;
        public VariableField<bool> returnValue = new();


        public sealed override State Execute()
        {
            if (node is not null)
            {
                return SetNextExecute(node);
            }
            else return StateOf(returnValue);
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref node);
        }
    }
}