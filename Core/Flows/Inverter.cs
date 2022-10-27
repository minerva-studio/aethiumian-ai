using Amlos.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI
{
    /// <summary>
    /// reverse the return value of the child node
    /// <br></br>
    /// return the inverse the return value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public class Inverter : Flow
    { 
        public NodeReference node;
         
        public override void End(bool @return)
        {
            base.End(!@return);
        }

        public override void Execute()
        {
            SetNextExecute(node);
        }

        public override void Initialize()
        {
            node = behaviourTree.References[node.uuid].ToReference();
        }
    }
}