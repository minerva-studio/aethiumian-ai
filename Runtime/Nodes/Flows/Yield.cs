namespace Aethiumian.AI.Nodes
{
    [NodeTip("Yield for only one frame")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Yield : Flow
    {
        bool? yield;

        public override State Execute()
        {
            if (yield.HasValue)
            {
                yield = null;
                return State.Success;
            }
            yield = true;
            return State.Yield;
        }

        public override void Initialize()
        {
            yield = null;
        }
    }
}
