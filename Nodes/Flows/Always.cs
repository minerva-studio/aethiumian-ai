using System;

namespace Amlos.AI
{

    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public sealed class Always : Flow
    {
        public NodeReference node;
        public VariableField<bool> returnValue = new();


        public sealed override void Execute()
        {
            //AddSelfToProgress();
            if (node is not null)
                SetNextExecute(node);
            else End(returnValue);
        }

        public override void Initialize()
        {
            node = behaviourTree.References[node.uuid].ToReference();
        }
    }
}