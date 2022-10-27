using System;

namespace Amlos.AI
{
    [NodeTip("Determine the executing node")]
    [Serializable]
    public class IsAtNode : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return behaviourTree.CurrentStage?.name ?? "";
        }
    }
}