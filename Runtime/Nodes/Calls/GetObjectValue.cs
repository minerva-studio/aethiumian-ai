using Amlos.AI.References;
using Amlos.AI.Variables;

namespace Amlos.AI.Nodes
{
    [NodeTip("Get value of a object")]
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
