using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;

namespace Amlos.AI.Nodes
{
    [NodeTip("Get value of a object")]
    public sealed class GetObjectValue : ObjectGetValueBase, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference type;

        public VariableReference Object => @object;
        public TypeReference TypeReference => type;

        public override State Execute()
        {
            object component = @object.Value;
            return GetValues(component);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as GetObjectValue).fieldPointers = fieldPointers.DeepCloneToList();
            return result;
        }
    }
}