using Minerva.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI
{

    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public class Always : Flow
    {
        public NodeReference node;
        public VariableField<bool> returnValue = new();
         

        public override void End(bool @return)
        {
            base.End(returnValue);
        }

        public override void Execute()
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