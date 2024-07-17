using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Pause the behaviour tree (Debug only)")]
    [Serializable]
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