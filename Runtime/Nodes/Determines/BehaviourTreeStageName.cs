using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Reads the current behaviour-tree stage name for comparison.")]
    [Serializable]
    public sealed class BehaviourTreeStageName : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage.name;
        }
    }
}
