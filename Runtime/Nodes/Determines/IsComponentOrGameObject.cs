using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Check variable refer to an component or an game object")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class IsComponentOrGameObject : Determine
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
            return variable.Value is Component or GameObject;
        }
    }
}
