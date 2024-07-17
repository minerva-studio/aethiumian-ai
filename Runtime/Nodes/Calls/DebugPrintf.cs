using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    public sealed class DebugPrintf : Call
    {
        public VariableField message;
        public VariableField value;
        public VariableReference<UnityEngine.Object> sender;
        public bool returnValue;

        public override State Execute()
        {
            //AddSelfToProgress();
            Debug.Log(string.Format(message.StringValue, value.Value), sender.GameObjectValue);
            return StateOf(returnValue);
        }
    }
}