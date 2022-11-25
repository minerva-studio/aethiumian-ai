using System;

namespace Amlos.AI
{
    [NodeTip("Determine the executing node")]
    [Serializable]
    public sealed class IsAtNode : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage?.name ?? "";
        }
    }
}