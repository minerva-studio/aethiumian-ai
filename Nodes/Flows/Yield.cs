namespace Amlos.AI.Nodes
{
    [NodeTip("Yield for only one frame")]
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
