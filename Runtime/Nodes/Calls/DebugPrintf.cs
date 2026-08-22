using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Writes a formatted message to the debug console.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class DebugPrintf : Call
    {
        [Readable] public VariableField message;
        [Readable] public VariableField value;
        [Readable] public VariableReference<UnityEngine.Object> sender;
        public bool returnValue;

        public override State Execute()
        {
            //AddSelfToProgress();
            Debug.Log(string.Format(message.StringValue, value.Value), sender.GameObjectValue);
            return StateOf(returnValue);
        }
    }
}
