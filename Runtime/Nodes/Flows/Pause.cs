using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Pause the behaviour tree (Debug only)")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Pause : Flow
    {
        public override State Execute()
        {
            behaviourTree.Pause();
            return State.Success;
        }


        public override void Initialize()
        {
        }
    }
}