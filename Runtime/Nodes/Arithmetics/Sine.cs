using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Sine : Arithmetic
    {
        [Readable]
        public VariableField a;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (a.IsNumeric)
                {
                    result.SetValue(Mathf.Sin(a.NumericValue));
                    return State.Success;
                }
                else
                    return State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
