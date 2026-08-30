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
            AI ai = behaviourTree?.AIComponent;
            if (ai == null)
            {
                UnityEngine.Debug.LogError("Pause node requires an AI owner.");
                return State.Failed;
            }

            ai.Pause();
            callStack?.StopAfterCurrentNode();
            return State.Success;
        }


        public override void Initialize()
        {
        }
    }
}