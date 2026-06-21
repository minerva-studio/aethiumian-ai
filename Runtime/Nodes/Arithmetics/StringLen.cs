using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class StringLen : Arithmetic
    {
        [Readable]
        public VariableField<string> a;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.SetValue(a.StringValue.Length);
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }
}