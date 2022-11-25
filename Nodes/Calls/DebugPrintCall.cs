using System;
using UnityEngine;

namespace Amlos.AI
{
    [AllowServiceCall]
    [NodeTip("A Debug-only node that prints message to the console")]
    [Serializable]
    public sealed class DebugPrintCall : Call
    {
        public VariableField message;
        public bool returnValue;


        public override void Execute()
        {
            //AddSelfToProgress();
            Debug.Log(message.Value);
            End(returnValue);
        }
    }
}