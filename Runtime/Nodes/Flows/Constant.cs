using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// execute the given node (if exist)
    /// return a constant value
    /// </summary>
    [Serializable]
    [NodeTip("Always return a fixed value")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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