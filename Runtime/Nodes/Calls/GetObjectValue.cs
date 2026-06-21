using Aethiumian.AI.References;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Get value of a object")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class GetObjectValue : ObjectGetValueBase, IObjectCaller
    {
        public VariableReference @object;
        public GenericTypeReference type;

        public VariableReference Object => @object;
        public TypeReference TypeReference => type;

        public override State Execute()
        {
            object component = @object.Value;
            return GetValues(component);
        }
    }
}
