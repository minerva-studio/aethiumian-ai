using System;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Get a component")]
    [Serializable]
    public sealed class GetComponent : Call
    {
        public TypeReference<Component> type;
        public VariableReference result;

        public override void Execute()
        {
            var component = gameObject.GetComponent(type.ReferType);
            if (result.HasRuntimeValue) result.Value = component;
            End(component);
        }
    }
}