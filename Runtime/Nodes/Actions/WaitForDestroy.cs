using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Wait until tracking object is destroyed")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class WaitForDestroy : Action
    {
        [Readable]
        public VariableReference<UnityEngine.Object> value;

        public override void Update()
        {
            if (!value.UnityObjectValue) Success();
        }
    }
}
