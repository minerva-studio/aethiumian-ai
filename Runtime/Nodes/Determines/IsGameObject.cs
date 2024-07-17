using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Check variable refer to an component or an game object")]
    public sealed class IsGameObject : Determine
    {
        public VariableReference variable;

        public override Exception IsValidNode()
        {
            if (!variable.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            return null;
        }

        public override bool GetValue()
        {
            return variable.Value is GameObject;
        }
    }
}
