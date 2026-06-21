using Aethiumian.AI.References;
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
    public sealed class Always : Flow
    {
        public NodeReference node;
        [Readable]
        public VariableField<bool> returnValue = new();


        public sealed override State Execute()
        {
            if (behaviourTree.GetNode(node) != null)
            {
                return SetNextExecute(node);
            }
            else return StateOf(returnValue);
        }

        public override void Initialize()
        {
        }
    }
}
