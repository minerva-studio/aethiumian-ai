using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Determine the main stack's executing node, typically used in an service")]
    [Serializable]
    public sealed class CurrentNodeName : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage.name;
        }
    }
}