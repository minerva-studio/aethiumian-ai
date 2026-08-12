using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Reads the executing node name from the current stack for comparison.")]
    [Serializable]
    public sealed class CurrentNodeName : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage.name;
        }
    }
}
