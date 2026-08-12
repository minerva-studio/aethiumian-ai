using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Checks whether a reference points to a Component.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class IsComponent : Determine
    {
        [Readable]
        public VariableReference variable;

        public override Exception IsValidNode()
        {
            if (!variable.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(variable), this);
            }
            return null;
        }

        public override bool GetValue()
        {
            return variable.Value is Component;
        }
    }
}
