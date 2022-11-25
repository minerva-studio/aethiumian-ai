using System;

namespace Amlos.AI
{
    [NodeTip("stage name of the behaivour tree")]
    [Serializable]
    public sealed class BehaviourTreeStageName : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage?.name ?? "";
        }
    }
}