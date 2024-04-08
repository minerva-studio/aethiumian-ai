using Amlos.AI.Variables;

namespace Amlos.AI.Nodes
{
    [NodeTip("Wait until tracking object is destroyed")]
    public class WaitForDestroy : Action
    {
        public VariableReference<UnityEngine.Object> value;

        public override void Update()
        {
            if (!value.UnityObjectValue) Success();
        }
    }
}
