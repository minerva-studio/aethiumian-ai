using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public sealed class Constant : Flow
    {
        public bool returnValue;


        public sealed override State Execute()
        {
            return StateOf(returnValue);
        }

        public override void Initialize()
        {
        }
    }
}