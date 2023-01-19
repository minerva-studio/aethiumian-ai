using Minerva.Module;

namespace Amlos.AI
{
    [NodeTip("Get value of a object")]
    public sealed class GetObjectValue : ObjectGetValueBase, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference type;

        public VariableReference Object => @object;
        public TypeReference TypeReference => type;

        public override void Execute()
        {
            object component = @object.Value;
            GetValues(component);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as GetObjectValue).fieldPointers = fieldPointers.DeepCloneToList();
            return result;
        }
    }
}