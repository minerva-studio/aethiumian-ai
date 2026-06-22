using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Set value of a object")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SetObjectValue : ObjectSetValueBase, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference<Component> type;

        public VariableReference Object => @object;
        public TypeReference TypeReference => type;

        public override State Execute()
        {
            object component = @object.Value;
            return SetValues(component);
        }

    }
}
