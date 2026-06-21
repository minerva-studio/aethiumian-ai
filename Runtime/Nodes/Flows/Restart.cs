using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Restart the current AI behaviour tree")]
    [Serializable]
    public sealed class Restart : Flow
    {
        public override State Execute()
        {
            // Reload recreates the runtime tree and follows the AI component's auto-restart setting.
            AIComponent.Reload();
            return State.NONE_RETURN;
        }

        public override void Initialize()
        {
        }
    }
}
