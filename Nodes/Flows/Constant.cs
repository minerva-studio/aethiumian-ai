using System;

namespace Amlos.AI
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


        public sealed override void Execute()
        {
            End(returnValue);
        }

        public override void Initialize()
        {
        }
    }
}